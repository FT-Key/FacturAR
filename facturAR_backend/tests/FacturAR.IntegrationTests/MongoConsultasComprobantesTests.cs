using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using FacturAR.Application.ApiKeys;
using FacturAR.Contracts.Comunes;
using FacturAR.Contracts.Comprobantes;
using FacturAR.Domain.Entities;
using FacturAR.Domain.Enums;
using FacturAR.Infrastructure.Mongo;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;

namespace FacturAR.IntegrationTests;

public sealed class MongoConsultasComprobantesTests : IAsyncLifetime
{
    private const string ApiKeyValor = "fak_0123456789ab_HHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHH";
    private const string ApiKeyPrefijo = "0123456789ab";
    private const string CuitEmisor = "20123456786";
    private const string CuitOtroContribuyente = "20111111112";
    private const string NombreIndiceListado = "ix_comprobantes_contribuyente_fecha_numero_id";

    private static readonly DateTime FechaOctubre = new(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime FechaFinOctubre = new(2026, 10, 31, 23, 59, 0, DateTimeKind.Utc);
    private static readonly DateTime FechaInicioNoviembre = new(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime FechaFinNoviembre = new(2026, 11, 30, 15, 0, 0, DateTimeKind.Utc);

    private readonly string _databaseName = $"facturar_it_{Guid.NewGuid():N}"[..28];
    private readonly ApiKeyHasher _apiKeyHasher = new();

    private long _caeContador;
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

        MongoSerialization.Configure();
        _factory = CrearFactory();
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
        await ApiKeys().InsertOneAsync(new ApiKey
        {
            ContribuyenteId = _contribuyenteA.Id,
            Nombre = "Consultas",
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
    public async Task CP01_ListadoSinFiltros_DevuelveSoloLosComprobantesDelContribuyenteAutenticado()
    {
        await LimpiarAsync();
        var ajeno = NuevoComprobante(_contribuyenteB!.Id, TipoComprobante.FacturaA, 1, FechaOctubre.AddMinutes(3));
        await SembrarAsync(
            NuevoComprobante(_contribuyenteA!.Id, TipoComprobante.FacturaA, 1, FechaOctubre),
            NuevoComprobante(_contribuyenteA.Id, TipoComprobante.FacturaA, 2, FechaOctubre.AddMinutes(1)),
            NuevoComprobante(_contribuyenteA.Id, TipoComprobante.FacturaA, 3, FechaOctubre.AddMinutes(2)),
            ajeno);

        var response = await GetAsync("/v1/comprobantes");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var pagina = await LeerAsync<PaginaResponse<ComprobanteResumenResponse>>(response);
        Assert.Equal(3, pagina.Total);
        Assert.Equal(1, pagina.TotalPages);
        Assert.Equal(3, pagina.Items.Count);
        Assert.DoesNotContain(pagina.Items, item => item.Id == ajeno.Id);
    }

    [MongoFact]
    public async Task CP02_PaginacionDeDosEnDos_NoRepiteNiSalteaComprobantes()
    {
        await LimpiarAsync();
        await SembrarAsync(
            NuevoComprobante(_contribuyenteA!.Id, TipoComprobante.FacturaA, 1, FechaInicioNoviembre),
            NuevoComprobante(_contribuyenteA.Id, TipoComprobante.FacturaA, 2, FechaInicioNoviembre.AddHours(1)),
            NuevoComprobante(_contribuyenteA.Id, TipoComprobante.FacturaA, 3, FechaInicioNoviembre.AddHours(2)));

        var primera = await LeerAsync<PaginaResponse<ComprobanteResumenResponse>>(
            await GetAsync("/v1/comprobantes?pageSize=2&page=1"));
        var segunda = await LeerAsync<PaginaResponse<ComprobanteResumenResponse>>(
            await GetAsync("/v1/comprobantes?pageSize=2&page=2"));

        Assert.Equal(3, primera.Total);
        Assert.Equal(2, primera.TotalPages);
        Assert.Equal(2, primera.Items.Count);
        Assert.Single(segunda.Items);

        var ids = primera.Items.Concat(segunda.Items).Select(item => item.Id).ToList();
        Assert.Equal(3, ids.Distinct().Count());
        Assert.Equal([3L, 2L, 1L], primera.Items.Concat(segunda.Items).Select(item => item.Numero).ToArray());
    }

    [MongoFact]
    public async Task CP03_VeinticincoComprobantes_SeRepartenEnPaginasDeDiezDiezYCincoSinRepetirIds()
    {
        await LimpiarAsync();
        var fecha = new DateTime(2026, 11, 15, 12, 0, 0, DateTimeKind.Utc);
        await SembrarAsync(Enumerable.Range(1, 25)
            .Select(numero => NuevoComprobante(
                _contribuyenteA!.Id,
                TipoComprobante.FacturaA,
                numero,
                fecha))
            .ToArray());

        var ids = new List<Guid>();
        var tamanios = new List<int>();
        for (var page = 1; page <= 3; page++)
        {
            var response = await GetAsync($"/v1/comprobantes?pageSize=10&page={page}");
            var pagina = await LeerAsync<PaginaResponse<ComprobanteResumenResponse>>(response);

            Assert.Equal(25, pagina.Total);
            Assert.Equal(3, pagina.TotalPages);
            tamanios.Add(pagina.Items.Count);
            ids.AddRange(pagina.Items.Select(item => item.Id));
        }

        Assert.Equal([10, 10, 5], tamanios);
        Assert.Equal(25, ids.Distinct().Count());
    }

    [MongoFact]
    public async Task CP04_RangoDeFechas_IncluyeElDiaCompletoDeHastaYExcluyeOctubre()
    {
        await LimpiarAsync();
        await SembrarAsync(
            NuevoComprobante(_contribuyenteA!.Id, TipoComprobante.FacturaA, 1, FechaOctubre),
            NuevoComprobante(_contribuyenteA.Id, TipoComprobante.FacturaA, 2, FechaFinOctubre),
            NuevoComprobante(_contribuyenteA.Id, TipoComprobante.FacturaA, 3, FechaInicioNoviembre),
            NuevoComprobante(_contribuyenteA.Id, TipoComprobante.FacturaA, 4, FechaFinNoviembre));

        var response = await GetAsync("/v1/comprobantes?desde=2026-11-01&hasta=2026-11-30");

        var pagina = await LeerAsync<PaginaResponse<ComprobanteResumenResponse>>(response);
        Assert.Equal(2, pagina.Total);
        Assert.Equal([4L, 3L], pagina.Items.Select(item => item.Numero).ToArray());
        Assert.Contains(pagina.Items, item => item.Numero == 4 && item.FechaEmision == FechaFinNoviembre);
    }

    [MongoFact]
    public async Task CP05_FiltroPorTipo_DevuelveSoloLasFacturasC()
    {
        await LimpiarAsync();
        await SembrarAsync(
            NuevoComprobante(_contribuyenteA!.Id, TipoComprobante.FacturaA, 1, FechaInicioNoviembre),
            NuevoComprobante(_contribuyenteA.Id, TipoComprobante.FacturaC, 1, FechaInicioNoviembre.AddHours(1)));

        var response = await GetAsync("/v1/comprobantes?tipo=11");

        var pagina = await LeerAsync<PaginaResponse<ComprobanteResumenResponse>>(response);
        Assert.Equal(1, pagina.Total);
        var item = Assert.Single(pagina.Items);
        Assert.Equal((int)TipoComprobante.FacturaC, item.TipoComprobante);
        Assert.Equal("Factura C", item.TipoComprobanteDescripcion);

        var invalido = await GetAsync("/v1/comprobantes?tipo=99");
        await AssertErrorAsync(invalido, HttpStatusCode.BadRequest, "VALIDACION");
    }

    [MongoFact]
    public async Task CP06_FiltroPorEstado_DevuelveSoloLosAnulados()
    {
        await LimpiarAsync();
        await SembrarAsync(
            NuevoComprobante(_contribuyenteA!.Id, TipoComprobante.FacturaA, 1, FechaInicioNoviembre),
            NuevoComprobante(
                _contribuyenteA.Id,
                TipoComprobante.FacturaA,
                2,
                FechaInicioNoviembre.AddHours(1),
                estado: EstadoComprobante.Anulado));

        var response = await GetAsync("/v1/comprobantes?estado=2");

        var pagina = await LeerAsync<PaginaResponse<ComprobanteResumenResponse>>(response);
        Assert.Equal(1, pagina.Total);
        var item = Assert.Single(pagina.Items);
        Assert.Equal(nameof(EstadoComprobante.Anulado), item.Estado);
        Assert.Equal(2, item.Numero);
    }

    [MongoFact]
    public async Task CP07_DetalleDeUnComprobantePropio_DevuelveImportesEItems()
    {
        await LimpiarAsync();
        var comprobante = NuevoComprobante(
            _contribuyenteA!.Id,
            TipoComprobante.FacturaA,
            42,
            FechaFinNoviembre,
            observaciones: "Detalle de prueba",
            items:
            [
                new ComprobanteItem
                {
                    Descripcion = "Servicio",
                    Cantidad = 2m,
                    PrecioUnitario = 5000m,
                    AlicuotaIva = 21m,
                    Subtotal = 10000m
                },
                new ComprobanteItem
                {
                    Descripcion = "Producto",
                    Cantidad = 1m,
                    PrecioUnitario = 2100m,
                    AlicuotaIva = 21m,
                    Subtotal = 2100m
                }
            ]);
        await SembrarAsync(comprobante);

        var response = await GetAsync($"/v1/comprobantes/{comprobante.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detalle = await LeerAsync<ComprobanteDetalleResponse>(response);
        Assert.Equal(comprobante.Id, detalle.Id);
        Assert.Equal(comprobante.Cae, detalle.Cae);
        Assert.Equal(42, detalle.Numero);
        Assert.Equal(12100m, detalle.ImporteTotal);
        Assert.Equal("Detalle de prueba", detalle.Observaciones);
        Assert.Equal(2, detalle.Items.Count);
        Assert.Equal("Servicio", detalle.Items[0].Descripcion);
        Assert.Equal(10000m, detalle.Items[0].Subtotal);
        Assert.Equal("Producto", detalle.Items[1].Descripcion);
        Assert.Equal(2100m, detalle.Items[1].Subtotal);
    }

    [MongoFact]
    public async Task CP08_DetalleDeUnComprobanteDeOtraEmpresa_Devuelve404SinRevelarDatos()
    {
        await LimpiarAsync();
        var ajeno = NuevoComprobante(_contribuyenteB!.Id, TipoComprobante.FacturaA, 1, FechaInicioNoviembre);
        await SembrarAsync(ajeno);

        var response = await GetAsync($"/v1/comprobantes/{ajeno.Id}");

        await AssertErrorAsync(response, HttpStatusCode.NotFound, "COMPROBANTE_NO_ENCONTRADO");
        var cuerpo = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Otra SRL", cuerpo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(ajeno.Cae, cuerpo, StringComparison.OrdinalIgnoreCase);
    }

    [MongoFact]
    public async Task CP09_DetalleDeUnIdInexistente_Devuelve404ComprobanteNoEncontrado()
    {
        await LimpiarAsync();

        var response = await GetAsync($"/v1/comprobantes/{Guid.NewGuid()}");

        await AssertErrorAsync(response, HttpStatusCode.NotFound, "COMPROBANTE_NO_ENCONTRADO");
    }

    [MongoFact]
    public async Task CP11_UltimoAutorizadoSinPuntoVenta_DevuelveElMaximoAgregadoPorTipo()
    {
        await LimpiarAsync();
        await SembrarSecuenciasAsync(42, 7);

        var response = await GetAsync("/v1/comprobantes/ultimo-autorizado?tipo=11");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var ultimo = await LeerAsync<UltimoAutorizadoResponse>(response);
        Assert.Equal((int)TipoComprobante.FacturaC, ultimo.TipoComprobante);
        Assert.Null(ultimo.PuntoVenta);
        Assert.Equal(42, ultimo.Numero);
    }

    [MongoFact]
    public async Task CP12_UltimoAutorizadoSinSecuencia_DevuelveCero()
    {
        await LimpiarAsync();

        var response = await GetAsync("/v1/comprobantes/ultimo-autorizado?tipo=2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var ultimo = await LeerAsync<UltimoAutorizadoResponse>(response);
        Assert.Equal(0, ultimo.Numero);
        Assert.Null(ultimo.PuntoVenta);
    }

    [MongoFact]
    public async Task CP13_UltimoAutorizadoConPuntoVenta_DevuelveLaSecuenciaDeEsePunto()
    {
        await LimpiarAsync();
        await SembrarSecuenciasAsync(42, 7);

        var response = await GetAsync("/v1/comprobantes/ultimo-autorizado?tipo=11&puntoVenta=2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var ultimo = await LeerAsync<UltimoAutorizadoResponse>(response);
        Assert.Equal((int)TipoComprobante.FacturaC, ultimo.TipoComprobante);
        Assert.Equal(2, ultimo.PuntoVenta);
        Assert.Equal(7, ultimo.Numero);
    }

    [MongoFact]
    public async Task InicializarIndices_CreaElIndiceDeListadoConOrdenEstable()
    {
        var indices = await (await Comprobantes().Indexes.ListAsync()).ToListAsync();
        var indice = Assert.Single(indices, i =>
            i.TryGetValue("name", out var name)
            && name.IsString
            && name.AsString == NombreIndiceListado);

        var claves = indice["key"].AsBsonDocument;
        Assert.Equal(
            ["ContribuyenteId", "FechaEmision", "Numero", "_id"],
            claves.Names.ToArray());
        Assert.Equal(1, claves["ContribuyenteId"].AsInt32);
        Assert.Equal(-1, claves["FechaEmision"].AsInt32);
        Assert.Equal(-1, claves["Numero"].AsInt32);
        Assert.Equal(1, claves["_id"].AsInt32);
    }

    private WebApplicationFactory<Program> CrearFactory()
        => new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.UseSetting("Mongo:ConnectionString", MongoTestSettings.GetConnectionStringWithTimeout());
                builder.UseSetting("Mongo:DatabaseName", _databaseName);
                builder.UseSetting("Mongo:InitializeIndexes", "true");
                builder.UseSetting("Outbox:Habilitado", "false");
                builder.UseSetting("ApiKeys:ActualizarUltimoUso", "false");
                builder.UseSetting("Seed:Enabled", "false");
                builder.UseSetting("BootstrapAdmin:Email", string.Empty);
                builder.UseSetting("Auth:PasswordHashIterations", "1000");
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

    private Task SembrarAsync(params Comprobante[] comprobantes)
        => Comprobantes().InsertManyAsync(comprobantes);

    private Task SembrarSecuenciasAsync(long primerPuntoVenta, long segundoPuntoVenta)
        => Secuencias().InsertManyAsync(
        [
            new Secuencia
            {
                ContribuyenteId = _contribuyenteA!.Id,
                PuntoVenta = 1,
                TipoComprobante = TipoComprobante.FacturaC,
                UltimoNumero = primerPuntoVenta
            },
            new Secuencia
            {
                ContribuyenteId = _contribuyenteA.Id,
                PuntoVenta = 2,
                TipoComprobante = TipoComprobante.FacturaC,
                UltimoNumero = segundoPuntoVenta
            }
        ]);

    private Comprobante NuevoComprobante(
        Guid contribuyenteId,
        TipoComprobante tipo,
        long numero,
        DateTime fecha,
        int puntoVenta = 1,
        EstadoComprobante estado = EstadoComprobante.Emitido,
        string? observaciones = null,
        IReadOnlyList<ComprobanteItem>? items = null)
        => new()
        {
            ContribuyenteId = contribuyenteId,
            EmisorCuit = CuitEmisor,
            EmisorRazonSocial = "Demo SRL",
            PuntoVenta = puntoVenta,
            TipoComprobante = tipo,
            Numero = numero,
            FechaEmision = fecha,
            Cae = Interlocked.Increment(ref _caeContador).ToString("D14", CultureInfo.InvariantCulture),
            CaeVencimiento = fecha.Date.AddDays(10),
            ReceptorDocumentoTipo = "CUIT",
            ReceptorDocumentoNumero = "20345678901",
            ReceptorRazonSocial = "Cliente S.A.",
            ReceptorCondicionIva = CondicionIva.ResponsableInscripto,
            ImporteNeto = 10000m,
            ImporteIva = 2100m,
            ImporteTotal = 12100m,
            Moneda = "ARS",
            Estado = estado,
            Observaciones = observaciones,
            Items = items?.ToList() ?? []
        };

    private async Task<HttpResponseMessage> GetAsync(string ruta)
    {
        using var mensaje = new HttpRequestMessage(HttpMethod.Get, ruta);
        mensaje.Headers.TryAddWithoutValidation("X-Api-Key", ApiKeyValor);

        return await Client.SendAsync(mensaje);
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

    private HttpClient Client => _client
        ?? throw new InvalidOperationException("El test de MongoDB no fue inicializado.");

    private IMongoCollection<Contribuyente> Contribuyentes()
        => Coleccion<Contribuyente>(MongoCollections.Contribuyentes);

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
