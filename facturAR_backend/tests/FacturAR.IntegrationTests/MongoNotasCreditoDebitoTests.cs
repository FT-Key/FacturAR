using System.Net;
using System.Net.Http.Json;
using FacturAR.Application.ApiKeys;
using FacturAR.Contracts.Comprobantes;
using FacturAR.Contracts.Comunes;
using FacturAR.Domain.Entities;
using FacturAR.Domain.Enums;
using FacturAR.Infrastructure.Mongo;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace FacturAR.IntegrationTests;

public sealed class MongoNotasCreditoDebitoTests : IAsyncLifetime
{
    private const string PrefijoA = "a1b2c3d4e5f6";
    private const string PrefijoB = "f6e5d4c3b2a1";
    private const string CuitEmisorA = "20123456786";
    private const string CuitEmisorB = "20111111112";
    private const string CuitReceptor = "30712345671";

    private static readonly string ApiKeyA = $"fak_{PrefijoA}_{new string('N', 43)}";
    private static readonly string ApiKeyB = $"fak_{PrefijoB}_{new string('M', 43)}";

    private readonly string _databaseName = $"facturar_it_{Guid.NewGuid():N}"[..28];
    private readonly ApiKeyHasher _hasher = new();

    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;
    private IMongoClient? _mongoClient;
    private Contribuyente? _empresaA;

    public async Task InitializeAsync()
    {
        if (string.IsNullOrWhiteSpace(MongoTestSettings.ConnectionString))
        {
            return;
        }

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.UseSetting("Mongo:ConnectionString", MongoTestSettings.GetConnectionStringWithTimeout());
                builder.UseSetting("Mongo:DatabaseName", _databaseName);
                builder.UseSetting("Mongo:InitializeIndexes", "true");
                builder.UseSetting("ApiKeys:ActualizarUltimoUso", "false");
                builder.UseSetting("Seed:Enabled", "false");
                builder.UseSetting("BootstrapAdmin:Email", string.Empty);
                builder.UseSetting("Auth:PasswordHashIterations", "1000");
            });
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false,
            AllowAutoRedirect = false
        });
        _mongoClient = _factory.Services.GetRequiredService<IMongoClient>();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await MongoIndexInitializer.InitializeAsync(_factory.Services.GetRequiredService<MongoDbContext>(), timeout.Token);

        _empresaA = new Contribuyente
        {
            RazonSocial = "Demo SRL",
            Cuit = CuitEmisorA,
            CondicionIva = CondicionIva.ResponsableInscripto,
            Activo = true
        };
        var empresaB = new Contribuyente
        {
            RazonSocial = "Otra SRL",
            Cuit = CuitEmisorB,
            CondicionIva = CondicionIva.ResponsableInscripto,
            Activo = true
        };
        await Coleccion<Contribuyente>(MongoCollections.Contribuyentes).InsertManyAsync([_empresaA, empresaB]);
        await Coleccion<PuntoVenta>(MongoCollections.PuntosVenta).InsertManyAsync(
        [
            new PuntoVenta { ContribuyenteId = _empresaA.Id, Numero = 1, Descripcion = "Principal", Activo = true },
            new PuntoVenta { ContribuyenteId = empresaB.Id, Numero = 1, Descripcion = "Principal", Activo = true }
        ]);
        await Coleccion<ApiKey>(MongoCollections.ApiKeys).InsertManyAsync(
        [
            new ApiKey { ContribuyenteId = _empresaA.Id, Nombre = "A", Prefix = PrefijoA, Hash = _hasher.Hash(ApiKeyA), Activa = true },
            new ApiKey { ContribuyenteId = empresaB.Id, Nombre = "B", Prefix = PrefijoB, Hash = _hasher.Hash(ApiKeyB), Activa = true }
        ]);
    }

    public Task DisposeAsync()
    {
        _mongoClient?.DropDatabase(_databaseName);
        _client?.Dispose();
        _factory?.Dispose();
        return Task.CompletedTask;
    }

    [MongoFact]
    public async Task CP0801_CP0815_CP0816_NotaDeCreditoA_CaePropioSaldoYSecuenciaImplicita()
    {
        var factura = await EmitirFacturaAAsync();

        var response = await EmitirAsync(Nota(TipoComprobante.NotaCreditoA, factura.Id, 1000m, 21m));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var nota = await Leer<ComprobanteResponse>(response);
        Assert.Equal("Nota de Crédito A", nota.TipoComprobanteDescripcion);
        Assert.Equal(1, nota.Numero);
        Assert.Equal(1000m, nota.ImporteNeto);
        Assert.Equal(210m, nota.ImporteIva);
        Assert.Equal(1210m, nota.ImporteTotal);
        Assert.Matches("^[1-9][0-9]{13}$", nota.Cae);
        Assert.NotEqual(factura.Cae, nota.Cae);

        var cuerpo = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("comprobanteOriginalId", cuerpo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("importeOriginalAjustado", cuerpo, StringComparison.OrdinalIgnoreCase);

        var original = await Obtener(factura.Id);
        Assert.Equal(10890m, original.ImporteOriginalAjustado);
        Assert.Equal(factura.Numero, original.Numero);
        Assert.Equal(factura.Cae, original.Cae);
        Assert.Equal(12100m, original.ImporteTotal);

        var guardada = await Obtener(nota.Id);
        Assert.Equal(factura.Id, guardada.ComprobanteOriginalId);
        Assert.Null(guardada.ImporteOriginalAjustado);

        var secuencia = await Coleccion<Secuencia>(MongoCollections.Secuencias)
            .Find(s => s.TipoComprobante == TipoComprobante.NotaCreditoA).SingleAsync();
        Assert.Equal(1, secuencia.UltimoNumero);

        Assert.Equal(1, await Coleccion<EventoAuditoria>(MongoCollections.Eventos)
            .CountDocumentsAsync(e => e.EntidadId == nota.Id.ToString() && e.Accion == "comprobante.emitido"));
    }

    [MongoFact]
    public async Task CP0802_CP0803_NotasDeCreditoByC_AjustanElSaldo()
    {
        var facturaB = await Leer<ComprobanteResponse>(await EmitirAsync(FacturaB()));
        var facturaC = await Leer<ComprobanteResponse>(await EmitirAsync(FacturaC()));

        Assert.Equal(HttpStatusCode.Created, (await EmitirAsync(NotaB(TipoComprobante.NotaCreditoB, facturaB.Id, 1210m))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await EmitirAsync(NotaC(TipoComprobante.NotaCreditoC, facturaC.Id, 1000m))).StatusCode);

        Assert.Equal(10890m, (await Obtener(facturaB.Id)).ImporteOriginalAjustado);
        Assert.Equal(9000m, (await Obtener(facturaC.Id)).ImporteOriginalAjustado);
    }

    [MongoFact]
    public async Task NotasDeDebitoByC_TienenCaePropioYAumentanElSaldo()
    {
        var facturaB = await Leer<ComprobanteResponse>(await EmitirAsync(FacturaB()));
        var facturaC = await Leer<ComprobanteResponse>(await EmitirAsync(FacturaC()));

        var debitoB = await Leer<ComprobanteResponse>(await EmitirAsync(NotaB(TipoComprobante.NotaDebitoB, facturaB.Id, 100m)));
        var debitoC = await Leer<ComprobanteResponse>(await EmitirAsync(NotaC(TipoComprobante.NotaDebitoC, facturaC.Id, 100m)));

        Assert.Equal("Nota de Débito B", debitoB.TipoComprobanteDescripcion);
        Assert.Equal("Nota de Débito C", debitoC.TipoComprobanteDescripcion);
        Assert.Equal(0m, debitoB.ImporteIva);
        Assert.Matches("^[1-9][0-9]{13}$", debitoB.Cae);
        Assert.Matches("^[1-9][0-9]{13}$", debitoC.Cae);
        Assert.Equal(12200m, (await Obtener(facturaB.Id)).ImporteOriginalAjustado);
        Assert.Equal(10100m, (await Obtener(facturaC.Id)).ImporteOriginalAjustado);
    }

    [MongoFact]
    public async Task CP0804_NotaASobreFacturaC_Devuelve400TipoNotaIncompatibleSinConsumirNumero()
    {
        var facturaC = await Leer<ComprobanteResponse>(await EmitirAsync(FacturaC()));

        await AssertError(
            await EmitirAsync(Nota(TipoComprobante.NotaCreditoA, facturaC.Id, 1000m, 21m)),
            HttpStatusCode.BadRequest,
            "TIPO_NOTA_INCOMPATIBLE");

        await AssertSinNotasNiSecuenciasDeNotaAsync();
    }

    [MongoFact]
    public async Task CP0805_EmpresaBSobreFacturaDeA_Devuelve404SinTocarElSaldo()
    {
        var factura = await EmitirFacturaAAsync();

        await AssertError(
            await EmitirAsync(Nota(TipoComprobante.NotaCreditoA, factura.Id, 1000m, 21m), ApiKeyB),
            HttpStatusCode.NotFound,
            "COMPROBANTE_NO_ENCONTRADO");

        Assert.Equal(12100m, (await Obtener(factura.Id)).ImporteOriginalAjustado);
        await AssertSinNotasNiSecuenciasDeNotaAsync();
    }

    [MongoFact]
    public async Task CP0806_CP0807_OriginalAnuladoORechazado_Devuelve409()
    {
        foreach (var estado in new[] { EstadoComprobante.Anulado, EstadoComprobante.Rechazado })
        {
            var factura = await EmitirFacturaAAsync();
            await Coleccion<Comprobante>(MongoCollections.Comprobantes).UpdateOneAsync(
                c => c.Id == factura.Id,
                Builders<Comprobante>.Update.Set(c => c.Estado, estado));

            await AssertError(
                await EmitirAsync(Nota(TipoComprobante.NotaCreditoA, factura.Id, 1000m, 21m)),
                HttpStatusCode.Conflict,
                "COMPROBANTE_ANULADO");
        }

        await AssertSinNotasNiSecuenciasDeNotaAsync();
    }

    [MongoFact]
    public async Task CP0808_CP0809_CP0819_SaldoExactoYExcedido()
    {
        var factura = await EmitirFacturaAAsync();
        await FijarSaldoAsync(factura.Id, 1000m);

        await AssertError(
            await EmitirAsync(Nota(TipoComprobante.NotaCreditoA, factura.Id, 1500m, 0m)),
            HttpStatusCode.BadRequest,
            "IMPORTE_EXCEDIDO");
        await AssertSinNotasNiSecuenciasDeNotaAsync();

        Assert.Equal(HttpStatusCode.Created, (await EmitirAsync(Nota(TipoComprobante.NotaCreditoA, factura.Id, 1000m, 0m))).StatusCode);
        Assert.Equal(0m, (await Obtener(factura.Id)).ImporteOriginalAjustado);

        await AssertError(
            await EmitirAsync(Nota(TipoComprobante.NotaCreditoA, factura.Id, 1m, 0m)),
            HttpStatusCode.BadRequest,
            "IMPORTE_EXCEDIDO");
    }

    [MongoFact]
    public async Task CP0810_CP0818_NotaDeDebito_AumentaElSaldoSinLimite()
    {
        var factura = await EmitirFacturaAAsync();

        var debito = await EmitirAsync(Nota(TipoComprobante.NotaDebitoA, factura.Id, 500m, 0m));
        Assert.Equal(HttpStatusCode.Created, debito.StatusCode);
        Assert.Matches("^[1-9][0-9]{13}$", (await Leer<ComprobanteResponse>(debito)).Cae);
        Assert.Equal(12600m, (await Obtener(factura.Id)).ImporteOriginalAjustado);

        await FijarSaldoAsync(factura.Id, 1000m);
        Assert.Equal(HttpStatusCode.Created, (await EmitirAsync(Nota(TipoComprobante.NotaDebitoA, factura.Id, 50000m, 0m))).StatusCode);
        Assert.Equal(51000m, (await Obtener(factura.Id)).ImporteOriginalAjustado);
    }

    [MongoFact]
    public async Task CP0811_CP0812_CP0813_ReferenciasInvalidas()
    {
        var factura = await EmitirFacturaAAsync();
        var nota = await Leer<ComprobanteResponse>(await EmitirAsync(Nota(TipoComprobante.NotaCreditoA, factura.Id, 1000m, 21m)));

        await AssertError(
            await EmitirAsync(Nota(TipoComprobante.NotaCreditoA, factura.Id, 1000m, 21m) with { ComprobanteOriginalId = null }),
            HttpStatusCode.BadRequest,
            "VALIDACION");
        await AssertError(
            await EmitirAsync(FacturaA() with { ComprobanteOriginalId = factura.Id }),
            HttpStatusCode.BadRequest,
            "VALIDACION");
        await AssertError(
            await EmitirAsync(Nota(TipoComprobante.NotaCreditoA, nota.Id, 100m, 21m)),
            HttpStatusCode.BadRequest,
            "TIPO_NOTA_INCOMPATIBLE");

        Assert.Equal(2, await Coleccion<Comprobante>(MongoCollections.Comprobantes).CountDocumentsAsync(FilterDefinition<Comprobante>.Empty));
    }

    [MongoFact]
    public async Task CP0814_DosNotasDeCreditoConcurrentes_SoloUnaPasaYElSaldoNuncaQuedaNegativo()
    {
        for (var ronda = 0; ronda < 3; ronda++)
        {
            var factura = await EmitirFacturaAAsync();

            var respuestas = await Task.WhenAll(Enumerable.Range(0, 2)
                .Select(_ => EmitirAsync(Nota(TipoComprobante.NotaCreditoA, factura.Id, 8000m, 0m))));

            Assert.Single(respuestas, r => r.StatusCode == HttpStatusCode.Created);
            var rechazada = Assert.Single(respuestas, r => r.StatusCode != HttpStatusCode.Created);
            await AssertError(rechazada, HttpStatusCode.BadRequest, "IMPORTE_EXCEDIDO");

            Assert.Equal(4100m, (await Obtener(factura.Id)).ImporteOriginalAjustado);
            Assert.Equal(1, await Coleccion<Comprobante>(MongoCollections.Comprobantes)
                .CountDocumentsAsync(c => c.ComprobanteOriginalId == factura.Id));
        }
    }

    [MongoFact]
    public async Task NotasConcurrentesQueEntranEnElSaldo_SeAplicanTodasSinPerderAjustes()
    {
        var factura = await EmitirFacturaAAsync();

        var respuestas = await Task.WhenAll(Enumerable.Range(0, 4)
            .Select(_ => EmitirAsync(Nota(TipoComprobante.NotaCreditoA, factura.Id, 1000m, 0m))));

        Assert.All(respuestas, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
        Assert.Equal(8100m, (await Obtener(factura.Id)).ImporteOriginalAjustado);
        var numeros = (await Task.WhenAll(respuestas.Select(Leer<ComprobanteResponse>))).Select(n => n.Numero).Order();
        Assert.Equal([1L, 2L, 3L, 4L], numeros);
    }

    [MongoFact]
    public async Task CP0817_LaFacturaYLaNotaSeVerificanCadaUnaPorSuCae()
    {
        var factura = await EmitirFacturaAAsync();
        var nota = await Leer<ComprobanteResponse>(await EmitirAsync(Nota(TipoComprobante.NotaCreditoA, factura.Id, 1000m, 21m)));

        var verificacionFactura = await _client!.GetFromJsonAsync<VerificacionResponse>($"/v1/comprobantes/verificar/{factura.Cae}");
        var verificacionNota = await _client.GetFromJsonAsync<VerificacionResponse>($"/v1/comprobantes/verificar/{nota.Cae}");

        Assert.Equal(factura.Cae, verificacionFactura!.Cae);
        Assert.Equal(12100m, verificacionFactura.ImporteTotal);
        Assert.Equal(nota.Cae, verificacionNota!.Cae);
        Assert.Equal(1210m, verificacionNota.ImporteTotal);
    }

    [MongoFact]
    public async Task FacturaSinSaldoGuardado_UsaElImporteTotal()
    {
        var factura = await EmitirFacturaAAsync();
        await Coleccion<Comprobante>(MongoCollections.Comprobantes).UpdateOneAsync(
            c => c.Id == factura.Id,
            Builders<Comprobante>.Update.Unset(c => c.ImporteOriginalAjustado));

        Assert.Equal(HttpStatusCode.Created, (await EmitirAsync(Nota(TipoComprobante.NotaCreditoA, factura.Id, 1000m, 21m))).StatusCode);
        Assert.Equal(10890m, (await Obtener(factura.Id)).ImporteOriginalAjustado);
    }

    private async Task<ComprobanteResponse> EmitirFacturaAAsync()
    {
        var response = await EmitirAsync(FacturaA());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var factura = await Leer<ComprobanteResponse>(response);
        Assert.Equal(12100m, (await Obtener(factura.Id)).ImporteOriginalAjustado);
        return factura;
    }

    private Task FijarSaldoAsync(Guid id, decimal saldo)
        => Coleccion<Comprobante>(MongoCollections.Comprobantes).UpdateOneAsync(
            c => c.Id == id,
            Builders<Comprobante>.Update.Set(c => c.ImporteOriginalAjustado, saldo));

    private async Task AssertSinNotasNiSecuenciasDeNotaAsync()
    {
        var tiposNota = new[]
        {
            TipoComprobante.NotaDebitoA, TipoComprobante.NotaCreditoA,
            TipoComprobante.NotaDebitoB, TipoComprobante.NotaCreditoB,
            TipoComprobante.NotaDebitoC, TipoComprobante.NotaCreditoC
        };
        Assert.Equal(0, await Coleccion<Comprobante>(MongoCollections.Comprobantes)
            .CountDocumentsAsync(Builders<Comprobante>.Filter.In(c => c.TipoComprobante, tiposNota)));
        Assert.Equal(0, await Coleccion<Secuencia>(MongoCollections.Secuencias)
            .CountDocumentsAsync(Builders<Secuencia>.Filter.In(s => s.TipoComprobante, tiposNota)));
    }

    private async Task<HttpResponseMessage> EmitirAsync(EmitirComprobanteRequest request, string? apiKey = null)
    {
        using var mensaje = new HttpRequestMessage(HttpMethod.Post, "/v1/comprobantes")
        {
            Content = JsonContent.Create(request)
        };
        mensaje.Headers.TryAddWithoutValidation("Idempotency-Key", $"idem-{Guid.NewGuid():N}");
        mensaje.Headers.TryAddWithoutValidation("X-Api-Key", apiKey ?? ApiKeyA);
        return await _client!.SendAsync(mensaje);
    }

    private static async Task<T> Leer<T>(HttpResponseMessage response)
        => (await response.Content.ReadFromJsonAsync<T>())
            ?? throw new InvalidOperationException($"La respuesta no contiene {typeof(T).Name}.");

    private static async Task AssertError(HttpResponseMessage response, HttpStatusCode status, string codigo)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal(codigo, (await Leer<ErrorResponse>(response)).Codigo);
    }

    private Task<Comprobante> Obtener(Guid id)
        => Coleccion<Comprobante>(MongoCollections.Comprobantes).Find(c => c.Id == id).SingleAsync();

    private IMongoCollection<T> Coleccion<T>(string nombre)
        => _mongoClient!.GetDatabase(_databaseName).GetCollection<T>(nombre);

    private static ReceptorComprobanteRequest ReceptorA()
        => new("CUIT", CuitReceptor, "Cliente S.A.", (int)CondicionIva.ResponsableInscripto);

    private static ReceptorComprobanteRequest ReceptorConsumidorFinal()
        => new("99", "30123456", "Cliente S.A.", (int)CondicionIva.ConsumidorFinal);

    private static EmitirComprobanteRequest FacturaA()
        => new((int)TipoComprobante.FacturaA, 1, null, ReceptorA(), [new ItemComprobanteRequest("Servicio", 2m, 5000m, 21m)], 12100m, null);

    private static EmitirComprobanteRequest FacturaB()
        => new((int)TipoComprobante.FacturaB, 1, null, ReceptorConsumidorFinal(), [new ItemComprobanteRequest("Producto", 1m, 12100m, 21m)], 12100m, null);

    private static EmitirComprobanteRequest FacturaC()
        => new((int)TipoComprobante.FacturaC, 1, null, new ReceptorComprobanteRequest("99", string.Empty, string.Empty, (int)CondicionIva.ConsumidorFinal), [new ItemComprobanteRequest("Producto", 1m, 10000m, 0m)], 10000m, null);

    private static EmitirComprobanteRequest Nota(TipoComprobante tipo, Guid originalId, decimal precio, decimal alicuota)
        => new((int)tipo, 1, null, ReceptorA(), [new ItemComprobanteRequest("Ajuste", 1m, precio, alicuota)], null, null, originalId);

    private static EmitirComprobanteRequest NotaB(TipoComprobante tipo, Guid originalId, decimal precio)
        => new((int)tipo, 1, null, ReceptorConsumidorFinal(), [new ItemComprobanteRequest("Ajuste", 1m, precio, 21m)], null, null, originalId);

    private static EmitirComprobanteRequest NotaC(TipoComprobante tipo, Guid originalId, decimal precio)
        => new((int)tipo, 1, null, new ReceptorComprobanteRequest("99", string.Empty, string.Empty, (int)CondicionIva.ConsumidorFinal), [new ItemComprobanteRequest("Ajuste", 1m, precio, 0m)], null, null, originalId);
}
