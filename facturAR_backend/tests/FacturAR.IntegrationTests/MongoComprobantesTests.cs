using System.Net;
using System.Net.Http.Json;
using FacturAR.Application.Abstractions;
using FacturAR.Application.ApiKeys;
using FacturAR.Application.Comprobantes;
using FacturAR.Contracts.Comprobantes;
using FacturAR.Contracts.Comunes;
using FacturAR.Domain.Entities;
using FacturAR.Domain.Enums;
using FacturAR.Domain.Exceptions;
using FacturAR.Infrastructure.Mongo;
using FacturAR.Infrastructure.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MongoDB.Driver;

namespace FacturAR.IntegrationTests;

public sealed class MongoComprobantesTests : IAsyncLifetime
{
    private const string ApiKeyValor = "fak_0123456789ab_CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC";
    private const string ApiKeyPrefijo = "0123456789ab";
    private const string ApiKeyDesconocida = "fak_ffffffffffff_DDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDD";
    private const string CuitEmisor = "20123456786";
    private const string CuitReceptor = "30712345671";
    private const string CuitReceptorAlterno = "27123456780";
    private const string CuitOtroContribuyente = "20111111112";

    private static readonly DateTime FechaEmision = new(2026, 10, 28, 15, 30, 0, DateTimeKind.Utc);

    private readonly string _databaseName = $"facturar_it_{Guid.NewGuid():N}"[..28];
    private readonly ApiKeyHasher _apiKeyHasher = new();

    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;
    private IMongoClient? _mongoClient;
    private Contribuyente? _contribuyenteA;
    private Contribuyente? _contribuyenteB;

    public async Task InitializeAsync()
    {
        if (string.IsNullOrWhiteSpace(MongoTestSettings.ConnectionString))
        {
            return;
        }

        _factory = CrearFactory(fallarEvento: false);
        _client = CrearCliente(_factory);
        _mongoClient = _factory.Services.GetRequiredService<IMongoClient>();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await MongoIndexInitializer.InitializeAsync(
            _factory.Services.GetRequiredService<MongoDbContext>(),
            timeout.Token);

        _contribuyenteA = new Contribuyente
        {
            RazonSocial = "Demo SRL",
            Cuit = CuitEmisor,
            CondicionIva = CondicionIva.ResponsableInscripto,
            Activo = true
        };
        _contribuyenteB = new Contribuyente
        {
            RazonSocial = "Otra SRL",
            Cuit = CuitOtroContribuyente,
            CondicionIva = CondicionIva.ResponsableInscripto,
            Activo = true
        };

        await Contribuyentes().InsertManyAsync([_contribuyenteA, _contribuyenteB]);

        await PuntosVenta().InsertManyAsync(
        [
            new PuntoVenta
            {
                ContribuyenteId = _contribuyenteA.Id,
                Numero = 1,
                Descripcion = "Principal",
                Activo = true
            },
            new PuntoVenta
            {
                ContribuyenteId = _contribuyenteA.Id,
                Numero = 2,
                Descripcion = "Inactivo",
                Activo = false
            },
            new PuntoVenta
            {
                ContribuyenteId = _contribuyenteB.Id,
                Numero = 9,
                Descripcion = "Ajeno",
                Activo = true
            }
        ]);

        await ApiKeys().InsertOneAsync(new ApiKey
        {
            ContribuyenteId = _contribuyenteA.Id,
            Nombre = "Emisión",
            Prefix = ApiKeyPrefijo,
            Hash = _apiKeyHasher.Hash(ApiKeyValor),
            Activa = true
        });
    }

    public Task DisposeAsync()
    {
        _mongoClient?.DropDatabase(_databaseName);
        _client?.Dispose();
        _factory?.Dispose();
        return Task.CompletedTask;
    }

    [MongoFact]
    public async Task CP0501_FacturaA_Devuelve201ConImportesNumeroCaeYVerificacionPorCae()
    {
        await LimpiarAsync();

        var response = await EmitirAsync(FacturaA(), "idem-cp0501");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var comprobante = await LeerAsync<ComprobanteResponse>(response);
        Assert.NotEqual(Guid.Empty, comprobante.Id);
        Assert.Equal((int)TipoComprobante.FacturaA, comprobante.TipoComprobante);
        Assert.Equal("Factura A", comprobante.TipoComprobanteDescripcion);
        Assert.Equal(1, comprobante.PuntoVenta);
        Assert.Equal(1, comprobante.Numero);
        Assert.Equal(FechaEmision, comprobante.FechaEmision);
        Assert.Matches("^[1-9][0-9]{13}$", comprobante.Cae);
        Assert.Equal(FechaEmision.Date.AddDays(10), comprobante.CaeVencimiento);
        Assert.Equal(10000m, comprobante.ImporteNeto);
        Assert.Equal(2100m, comprobante.ImporteIva);
        Assert.Equal(12100m, comprobante.ImporteTotal);
        Assert.Equal("ARS", comprobante.Moneda);
        Assert.Equal("Emitido", comprobante.Estado);

        var guardado = await Comprobantes().Find(c => c.Cae == comprobante.Cae).SingleAsync();
        Assert.Equal(_contribuyenteA!.Id, guardado.ContribuyenteId);
        Assert.Equal("idem-cp0501", guardado.IdempotencyKey);
        Assert.Equal(1, guardado.Numero);
        Assert.Equal(EstadoComprobante.Emitido, guardado.Estado);
        Assert.Equal(DateTimeKind.Utc, guardado.FechaEmision.Kind);
        Assert.Equal(DateTimeKind.Utc, guardado.CaeVencimiento.Kind);
        Assert.Equal(CuitEmisor, guardado.EmisorCuit);
        Assert.Equal("Demo SRL", guardado.EmisorRazonSocial);

        var verificacion = await Client.GetAsync($"/v1/comprobantes/verificar/{comprobante.Cae}");
        Assert.Equal(HttpStatusCode.OK, verificacion.StatusCode);
        var verificado = await LeerAsync<VerificacionResponse>(verificacion);
        Assert.Equal(comprobante.Cae, verificado.Cae);
        Assert.Equal(CuitEmisor, verificado.CuitEmisor);
        Assert.Equal(1, verificado.PuntoVenta);
        Assert.Equal(1, verificado.Numero);
        Assert.Equal(12100m, verificado.ImporteTotal);
        Assert.Equal("Emitido", verificado.Estado);
    }

    [MongoFact]
    public async Task CP0502_FacturaC_Devuelve201SinDiscriminarIva()
    {
        await LimpiarAsync();

        var response = await EmitirAsync(FacturaC(), "idem-cp0502");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var comprobante = await LeerAsync<ComprobanteResponse>(response);
        Assert.Equal((int)TipoComprobante.FacturaC, comprobante.TipoComprobante);
        Assert.Equal("Factura C", comprobante.TipoComprobanteDescripcion);
        Assert.Equal(1, comprobante.Numero);
        Assert.Equal(10000m, comprobante.ImporteNeto);
        Assert.Equal(0m, comprobante.ImporteIva);
        Assert.Equal(10000m, comprobante.ImporteTotal);
        Assert.Equal("Emitido", comprobante.Estado);
        Assert.Matches("^[1-9][0-9]{13}$", comprobante.Cae);
    }

    [MongoFact]
    public async Task CP0503_FacturaB_Devuelve201ConElIvaIncluidoEnElPrecio()
    {
        await LimpiarAsync();

        var response = await EmitirAsync(FacturaB(), "idem-cp0503");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var comprobante = await LeerAsync<ComprobanteResponse>(response);
        Assert.Equal((int)TipoComprobante.FacturaB, comprobante.TipoComprobante);
        Assert.Equal("Factura B", comprobante.TipoComprobanteDescripcion);
        Assert.Equal(12100m, comprobante.ImporteNeto);
        Assert.Equal(0m, comprobante.ImporteIva);
        Assert.Equal(12100m, comprobante.ImporteTotal);
    }

    [MongoFact]
    public async Task CP0504_ReceptorConDocumentoInvalido_Devuelve400CuitInvalidoSinConsumirNumero()
    {
        await LimpiarAsync();
        var request = FacturaA() with
        {
            Receptor = new ReceptorComprobanteRequest(
                "CUIT",
                "123456789",
                "Cliente S.A.",
                (int)CondicionIva.ResponsableInscripto)
        };

        var response = await EmitirAsync(request, "idem-cp0504");

        await AssertErrorAsync(response, HttpStatusCode.BadRequest, "CUIT_INVALIDO");
        await AssertSinConsumoAsync();
    }

    [MongoFact]
    public async Task CP0505_ReceptorConsumidorFinalEnFacturaA_Devuelve400ValidacionSinConsumirNumero()
    {
        await LimpiarAsync();
        var request = FacturaA() with
        {
            Receptor = new ReceptorComprobanteRequest(
                "CUIT",
                CuitReceptor,
                "Cliente S.A.",
                (int)CondicionIva.ConsumidorFinal)
        };

        var response = await EmitirAsync(request, "idem-cp0505");

        await AssertErrorAsync(response, HttpStatusCode.BadRequest, "VALIDACION");
        await AssertSinConsumoAsync();
    }

    [MongoFact]
    public async Task CP0506_ImporteTotalInconsistente_Devuelve400ValidacionYLaSecuenciaNoAvanza()
    {
        await LimpiarAsync();
        await SembrarSecuenciaAsync(TipoComprobante.FacturaA, 5);

        var response = await EmitirAsync(FacturaA(importeTotal: 9999m), "idem-cp0506");

        await AssertErrorAsync(response, HttpStatusCode.BadRequest, "VALIDACION");
        Assert.Equal(0, await Comprobantes().CountDocumentsAsync(FilterDefinition<Comprobante>.Empty));
        Assert.Equal(5, (await SecuenciaAsync(TipoComprobante.FacturaA)).UltimoNumero);
    }

    [MongoFact]
    public async Task CP0507_PuntoDeVentaDeOtroContribuyente_Devuelve404SinCrearComprobante()
    {
        await LimpiarAsync();

        var response = await EmitirAsync(FacturaA() with { PuntoVenta = 9 }, "idem-cp0507");

        await AssertErrorAsync(response, HttpStatusCode.NotFound, "COMPROBANTE_NO_ENCONTRADO");
        Assert.Equal(0, await Comprobantes().CountDocumentsAsync(FilterDefinition<Comprobante>.Empty));
        Assert.Equal(0, await Secuencias().CountDocumentsAsync(FilterDefinition<Secuencia>.Empty));
    }

    [MongoFact]
    public async Task CP0508_PuntoDeVentaInactivo_Devuelve409SinCrearComprobante()
    {
        await LimpiarAsync();

        var response = await EmitirAsync(FacturaA() with { PuntoVenta = 2 }, "idem-cp0508");

        await AssertErrorAsync(response, HttpStatusCode.Conflict, "PUNTO_VENTA_INACTIVO");
        Assert.Equal(0, await Comprobantes().CountDocumentsAsync(FilterDefinition<Comprobante>.Empty));
        Assert.Equal(0, await Secuencias().CountDocumentsAsync(FilterDefinition<Secuencia>.Empty));
    }

    [MongoFact]
    public async Task CP0509_FacturaCSinSecuencia_CreaLaSecuenciaImplicitaConUltimoNumeroUno()
    {
        await LimpiarAsync();

        var response = await EmitirAsync(FacturaC(), "idem-cp0509");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var comprobante = await LeerAsync<ComprobanteResponse>(response);
        Assert.Equal(1, comprobante.Numero);

        var secuencia = await SecuenciaAsync(TipoComprobante.FacturaC);
        Assert.Equal(1, secuencia.UltimoNumero);
        Assert.NotEqual(Guid.Empty, secuencia.Id);
    }

    [MongoFact]
    public async Task CP0511_ContribuyenteInactivo_LanzaCuentaInactivaSinConsumirNumero()
    {
        await LimpiarAsync();
        var contribuyenteId = _contribuyenteA!.Id;
        await Contribuyentes().UpdateOneAsync(
            c => c.Id == contribuyenteId,
            Builders<Contribuyente>.Update.Set(c => c.Activo, false));

        try
        {
            using var scope = _factory!.Services.CreateScope();
            var servicio = scope.ServiceProvider.GetRequiredService<IEmitirComprobanteService>();

            await Assert.ThrowsAsync<CuentaInactivaException>(() => servicio.EmitirAsync(
                contribuyenteId,
                "Emisor",
                FacturaA(),
                "idem-cp0511"));

            await AssertSinConsumoAsync();
        }
        finally
        {
            await Contribuyentes().UpdateOneAsync(
                c => c.Id == contribuyenteId,
                Builders<Contribuyente>.Update.Set(c => c.Activo, true));
        }
    }

    [MongoFact]
    public async Task CP0514_ApiKeyConPrefijoInexistente_Devuelve401ApiKeyInvalida()
    {
        await LimpiarAsync();

        var response = await EmitirAsync(FacturaA(), "idem-cp0514", ApiKeyDesconocida);

        await AssertErrorAsync(response, HttpStatusCode.Unauthorized, "API_KEY_INVALIDA");
        Assert.Equal(0, await Comprobantes().CountDocumentsAsync(FilterDefinition<Comprobante>.Empty));
    }

    [MongoFact]
    public async Task CP0515_CantidadCeroOPrecioNegativo_Devuelve400ValidacionSinConsumirNumero()
    {
        await LimpiarAsync();
        var cantidadCero = FacturaA() with
        {
            ImporteTotal = null,
            Items = [new ItemComprobanteRequest("Servicio", 0m, 5000m, 21m)]
        };
        var precioNegativo = FacturaA() with
        {
            ImporteTotal = null,
            Items = [new ItemComprobanteRequest("Servicio", 1m, -1m, 21m)]
        };

        var responseCantidad = await EmitirAsync(cantidadCero, "idem-cp0515-cantidad");
        var responsePrecio = await EmitirAsync(precioNegativo, "idem-cp0515-precio");

        await AssertErrorAsync(responseCantidad, HttpStatusCode.BadRequest, "VALIDACION");
        await AssertErrorAsync(responsePrecio, HttpStatusCode.BadRequest, "VALIDACION");
        await AssertSinConsumoAsync();
    }

    [MongoFact]
    public async Task CP0516_AlicuotaNoPermitida_Devuelve400ValidacionSinConsumirNumero()
    {
        await LimpiarAsync();
        var request = FacturaA() with
        {
            ImporteTotal = null,
            Items = [new ItemComprobanteRequest("Servicio", 1m, 1000m, 12m)]
        };

        var response = await EmitirAsync(request, "idem-cp0516");

        await AssertErrorAsync(response, HttpStatusCode.BadRequest, "VALIDACION");
        await AssertSinConsumoAsync();
    }

    [MongoFact]
    public async Task CP0517_FacturaCConAlicuotaVeintiuno_Devuelve201SinIva()
    {
        await LimpiarAsync();
        var request = FacturaC() with
        {
            Items = [new ItemComprobanteRequest("Producto", 1m, 10000m, 21m)]
        };

        var response = await EmitirAsync(request, "idem-cp0517");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var comprobante = await LeerAsync<ComprobanteResponse>(response);
        Assert.Equal(10000m, comprobante.ImporteNeto);
        Assert.Equal(0m, comprobante.ImporteIva);
        Assert.Equal(10000m, comprobante.ImporteTotal);
    }

    [MongoFact]
    public async Task CP0518_DosEmisionesConcurrentes_DevuelvenNumerosConsecutivosYCaeDistintos()
    {
        await LimpiarAsync();
        await SembrarSecuenciaAsync(TipoComprobante.FacturaA, 0);

        var respuestas = await Task.WhenAll(
            EmitirAsync(FacturaA(), "idem-cp0518-1"),
            EmitirAsync(FacturaA(), "idem-cp0518-2"));

        Assert.All(respuestas, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));

        var comprobantes = new List<ComprobanteResponse>();
        foreach (var respuesta in respuestas)
        {
            comprobantes.Add(await LeerAsync<ComprobanteResponse>(respuesta));
        }

        long[] esperados = [1L, 2L];
        Assert.Equal(esperados, comprobantes.Select(c => c.Numero).Order().ToArray());
        Assert.Equal(2, comprobantes.Select(c => c.Cae).Distinct().Count());
        Assert.Equal(2, await Comprobantes().CountDocumentsAsync(FilterDefinition<Comprobante>.Empty));
        Assert.Equal(2, (await SecuenciaAsync(TipoComprobante.FacturaA)).UltimoNumero);
    }

    [MongoFact]
    public async Task CP0608_DosRequestsSimultaneosConMismaClaveYBody_GeneranUnSoloComprobante()
    {
        await LimpiarAsync();

        var respuestas = await Task.WhenAll(
            EmitirAsync(FacturaA(), "idem-cp0608"),
            EmitirAsync(FacturaA(), "idem-cp0608"));

        var estados = respuestas.Select(r => r.StatusCode).OrderBy(s => (int)s).ToArray();
        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.Created], estados);

        var comprobantes = new List<ComprobanteResponse>();
        foreach (var respuesta in respuestas)
        {
            comprobantes.Add(await LeerAsync<ComprobanteResponse>(respuesta));
        }

        Assert.Single(comprobantes.Select(c => c.Id).Distinct());
        Assert.Single(comprobantes.Select(c => c.Numero).Distinct());
        Assert.Single(comprobantes.Select(c => c.Cae).Distinct());
        Assert.Equal(1, await Comprobantes().CountDocumentsAsync(FilterDefinition<Comprobante>.Empty));
        Assert.Equal(1, (await SecuenciaAsync(TipoComprobante.FacturaA)).UltimoNumero);
    }

    [MongoFact]
    public async Task CP0609_DosRequestsSimultaneosConMismaClaveYBodyDistinto_Devuelven201Y409()
    {
        await LimpiarAsync();

        var distinto = FacturaA() with
        {
            ImporteTotal = 24200m,
            Items = [new ItemComprobanteRequest("Servicio", 2m, 10000m, 21m)]
        };

        var respuestas = await Task.WhenAll(
            EmitirAsync(FacturaA(), "idem-cp0609"),
            EmitirAsync(distinto, "idem-cp0609"));

        var estados = respuestas.Select(r => r.StatusCode).OrderBy(s => (int)s).ToArray();
        Assert.Equal([HttpStatusCode.Created, HttpStatusCode.Conflict], estados);
        Assert.Equal(1, await Comprobantes().CountDocumentsAsync(FilterDefinition<Comprobante>.Empty));
        Assert.Equal(1, (await SecuenciaAsync(TipoComprobante.FacturaA)).UltimoNumero);
    }

    [MongoFact]
    public async Task CP0611_ReintentosRepetidos_DevuelvenSiempreElMismoComprobanteYPersistenElHash()
    {
        await LimpiarAsync();
        var request = FacturaA();

        var primero = await EmitirAsync(request, "idem-cp0611");
        Assert.Equal(HttpStatusCode.Created, primero.StatusCode);
        var original = await LeerAsync<ComprobanteResponse>(primero);

        for (var intento = 0; intento < 5; intento++)
        {
            var repetido = await EmitirAsync(request, "idem-cp0611");
            Assert.Equal(HttpStatusCode.OK, repetido.StatusCode);
            var comprobante = await LeerAsync<ComprobanteResponse>(repetido);
            Assert.Equal(original.Id, comprobante.Id);
            Assert.Equal(original.Numero, comprobante.Numero);
            Assert.Equal(original.Cae, comprobante.Cae);
        }

        Assert.Equal(1, await Comprobantes().CountDocumentsAsync(FilterDefinition<Comprobante>.Empty));
        Assert.Equal(1, (await SecuenciaAsync(TipoComprobante.FacturaA)).UltimoNumero);

        var guardado = await Comprobantes()
            .Find(c => c.IdempotencyKey == "idem-cp0611")
            .SingleAsync();
        Assert.Equal(PayloadHasher.Hash(request), guardado.IdempotencyPayloadHash);
        Assert.Matches("^[0-9a-f]{64}$", guardado.IdempotencyPayloadHash);
    }

    [MongoFact]
    public async Task CP0612_ClaveRegistradaTiempoAtras_NoExpiraYDevuelveElMismoComprobante()
    {
        await LimpiarAsync();
        var request = FacturaA();

        var primero = await EmitirAsync(request, "idem-cp0612");
        Assert.Equal(HttpStatusCode.Created, primero.StatusCode);
        var original = await LeerAsync<ComprobanteResponse>(primero);

        await Comprobantes().UpdateOneAsync(
            c => c.IdempotencyKey == "idem-cp0612",
            Builders<Comprobante>.Update.Set(c => c.CreatedAt, DateTime.UtcNow.AddYears(-2)));

        var repetido = await EmitirAsync(request, "idem-cp0612");
        Assert.Equal(HttpStatusCode.OK, repetido.StatusCode);
        var comprobante = await LeerAsync<ComprobanteResponse>(repetido);
        Assert.Equal(original.Id, comprobante.Id);
        Assert.Equal(original.Numero, comprobante.Numero);
        Assert.Equal(original.Cae, comprobante.Cae);
    }

    [MongoFact]
    public async Task CP0520_FacturaAConReceptorResponsableInscripto_Devuelve201ConIvaDiscriminado()
    {
        await LimpiarAsync();
        var request = FacturaA() with
        {
            Receptor = new ReceptorComprobanteRequest(
                "CUIT",
                CuitReceptorAlterno,
                "Cliente Alterno S.A.",
                (int)CondicionIva.ResponsableInscripto)
        };

        var response = await EmitirAsync(request, "idem-cp0520");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var comprobante = await LeerAsync<ComprobanteResponse>(response);
        Assert.Equal(10000m, comprobante.ImporteNeto);
        Assert.Equal(2100m, comprobante.ImporteIva);
        Assert.Equal(12100m, comprobante.ImporteTotal);
        Assert.Equal(CuitReceptorAlterno, comprobante.Receptor.DocumentoNumero);
        Assert.Equal((int)CondicionIva.ResponsableInscripto, comprobante.Receptor.CondicionIva);
    }

    [MongoFact]
    public async Task Rollback_SiFallaLaAuditoria_NoPersisteElComprobanteNiConsumeElNumero()
    {
        await LimpiarAsync();
        await SembrarSecuenciaAsync(TipoComprobante.FacturaA, 3);

        using var factory = CrearFactory(fallarEvento: true);
        using var client = CrearCliente(factory);

        var response = await EmitirAsync(client, FacturaA(), "idem-rollback");

        await AssertErrorAsync(response, HttpStatusCode.InternalServerError, "ERROR_INTERNO");
        Assert.Equal(0, await Comprobantes().CountDocumentsAsync(FilterDefinition<Comprobante>.Empty));
        Assert.Equal(3, (await SecuenciaAsync(TipoComprobante.FacturaA)).UltimoNumero);
        Assert.Equal(0, await Eventos().CountDocumentsAsync(FilterDefinition<EventoAuditoria>.Empty));
    }

    [MongoFact]
    public async Task IndiceUxCae_RechazaUnCaeDuplicadoInsertadoDirectamente()
    {
        await LimpiarAsync();
        await Comprobantes().InsertOneAsync(NuevoComprobante("12345678901234", 1));

        var ex = await Assert.ThrowsAsync<MongoWriteException>(
            () => Comprobantes().InsertOneAsync(NuevoComprobante("12345678901234", 2)));

        Assert.Equal(11000, ex.WriteError.Code);
        Assert.Equal(1, await Comprobantes().CountDocumentsAsync(FilterDefinition<Comprobante>.Empty));
    }

    private WebApplicationFactory<Program> CrearFactory(bool fallarEvento)
        => new WebApplicationFactory<Program>()
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

                if (fallarEvento)
                {
                    builder.ConfigureTestServices(services =>
                    {
                        services.RemoveAll<IEventoAuditoriaRepository>();
                        services.AddScoped<IEventoAuditoriaRepository>(sp => new EventoAuditoriaQueFalla(
                            new EventoAuditoriaRepository(
                                sp.GetRequiredService<MongoDbContext>(),
                                sp.GetRequiredService<MongoSesionActual>())));
                    });
                }
            });

    private static HttpClient CrearCliente(WebApplicationFactory<Program> factory)
        => factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false,
            AllowAutoRedirect = false
        });

    private async Task LimpiarAsync()
    {
        await Comprobantes().DeleteManyAsync(FilterDefinition<Comprobante>.Empty);
        await Secuencias().DeleteManyAsync(FilterDefinition<Secuencia>.Empty);
        await Eventos().DeleteManyAsync(FilterDefinition<EventoAuditoria>.Empty);
    }

    private async Task AssertSinConsumoAsync()
    {
        Assert.Equal(0, await Comprobantes().CountDocumentsAsync(FilterDefinition<Comprobante>.Empty));
        Assert.Equal(0, await Secuencias().CountDocumentsAsync(FilterDefinition<Secuencia>.Empty));
    }

    private Task<HttpResponseMessage> EmitirAsync(
        EmitirComprobanteRequest request,
        string idempotencyKey,
        string? apiKey = null)
        => EmitirAsync(Client, request, idempotencyKey, apiKey);

    private static async Task<HttpResponseMessage> EmitirAsync(
        HttpClient client,
        EmitirComprobanteRequest request,
        string idempotencyKey,
        string? apiKey = null)
    {
        using var mensaje = new HttpRequestMessage(HttpMethod.Post, "/v1/comprobantes")
        {
            Content = JsonContent.Create(request)
        };
        mensaje.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        mensaje.Headers.TryAddWithoutValidation("X-Api-Key", apiKey ?? ApiKeyValor);

        return await client.SendAsync(mensaje);
    }

    private static async Task<T> LeerAsync<T>(HttpResponseMessage response)
        => (await response.Content.ReadFromJsonAsync<T>())
            ?? throw new InvalidOperationException($"La respuesta no contiene {typeof(T).Name}.");

    private static async Task<ErrorResponse> AssertErrorAsync(
        HttpResponseMessage response,
        HttpStatusCode status,
        string codigo)
    {
        Assert.Equal(status, response.StatusCode);
        var error = await LeerAsync<ErrorResponse>(response);
        Assert.Equal(codigo, error.Codigo);
        return error;
    }

    private static EmitirComprobanteRequest FacturaA(
        DateTime? fechaEmision = null,
        decimal? importeTotal = 12100m,
        int puntoVenta = 1)
        => new(
            (int)TipoComprobante.FacturaA,
            puntoVenta,
            fechaEmision ?? FechaEmision,
            new ReceptorComprobanteRequest(
                "CUIT",
                CuitReceptor,
                "Cliente S.A.",
                (int)CondicionIva.ResponsableInscripto),
            [new ItemComprobanteRequest("Servicio", 2m, 5000m, 21m)],
            importeTotal,
            null);

    private static EmitirComprobanteRequest FacturaB()
        => new(
            (int)TipoComprobante.FacturaB,
            1,
            FechaEmision,
            new ReceptorComprobanteRequest(
                "99",
                "30123456",
                "Cliente S.A.",
                (int)CondicionIva.ConsumidorFinal),
            [new ItemComprobanteRequest("Producto", 1m, 12100m, 21m)],
            12100m,
            null);

    private static EmitirComprobanteRequest FacturaC()
        => new(
            (int)TipoComprobante.FacturaC,
            1,
            FechaEmision,
            new ReceptorComprobanteRequest(
                "99",
                string.Empty,
                string.Empty,
                (int)CondicionIva.ConsumidorFinal),
            [new ItemComprobanteRequest("Producto", 1m, 10000m, 0m)],
            10000m,
            null);

    private Comprobante NuevoComprobante(string cae, long numero)
        => new()
        {
            ContribuyenteId = _contribuyenteA!.Id,
            EmisorCuit = CuitEmisor,
            EmisorRazonSocial = "Demo SRL",
            PuntoVenta = 1,
            TipoComprobante = TipoComprobante.FacturaA,
            Numero = numero,
            FechaEmision = FechaEmision,
            Cae = cae,
            CaeVencimiento = FechaEmision.Date.AddDays(10),
            ImporteNeto = 100m,
            ImporteIva = 21m,
            ImporteTotal = 121m,
            IdempotencyKey = $"idem-indice-{numero}"
        };

    private Task SembrarSecuenciaAsync(TipoComprobante tipo, long ultimoNumero)
        => Secuencias().InsertOneAsync(new Secuencia
        {
            ContribuyenteId = _contribuyenteA!.Id,
            PuntoVenta = 1,
            TipoComprobante = tipo,
            UltimoNumero = ultimoNumero
        });

    private Task<Secuencia> SecuenciaAsync(TipoComprobante tipo)
    {
        var filtro = Builders<Secuencia>.Filter.Eq(s => s.ContribuyenteId, _contribuyenteA!.Id)
            & Builders<Secuencia>.Filter.Eq(s => s.PuntoVenta, 1)
            & Builders<Secuencia>.Filter.Eq(s => s.TipoComprobante, tipo);

        return Secuencias().Find(filtro).SingleAsync();
    }

    private HttpClient Client => _client
        ?? throw new InvalidOperationException("El test de MongoDB no fue inicializado.");

    private IMongoCollection<Contribuyente> Contribuyentes()
        => Coleccion<Contribuyente>(MongoCollections.Contribuyentes);

    private IMongoCollection<PuntoVenta> PuntosVenta()
        => Coleccion<PuntoVenta>(MongoCollections.PuntosVenta);

    private IMongoCollection<ApiKey> ApiKeys()
        => Coleccion<ApiKey>(MongoCollections.ApiKeys);

    private IMongoCollection<Secuencia> Secuencias()
        => Coleccion<Secuencia>(MongoCollections.Secuencias);

    private IMongoCollection<Comprobante> Comprobantes()
        => Coleccion<Comprobante>(MongoCollections.Comprobantes);

    private IMongoCollection<EventoAuditoria> Eventos()
        => Coleccion<EventoAuditoria>(MongoCollections.Eventos);

    private IMongoCollection<T> Coleccion<T>(string nombre)
        => _mongoClient!.GetDatabase(_databaseName).GetCollection<T>(nombre);
}

internal sealed class EventoAuditoriaQueFalla : IEventoAuditoriaRepository
{
    private readonly EventoAuditoriaRepository _inner;

    public EventoAuditoriaQueFalla(EventoAuditoriaRepository inner)
    {
        _inner = inner;
    }

    public Task AddAsync(EventoAuditoria evento, CancellationToken ct = default)
        => throw new InvalidOperationException("Falla forzada de la prueba de rollback.");

    public Task<IReadOnlyList<EventoAuditoria>> ListarPorEntidadAsync(
        string entidad,
        string entidadId,
        CancellationToken ct = default)
        => _inner.ListarPorEntidadAsync(entidad, entidadId, ct);
}
