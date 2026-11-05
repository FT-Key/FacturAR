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

public sealed class MongoDashboardTests : IAsyncLifetime
{
    private const string ApiKeyValor = "fak_0123456789ab_IIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIIII";
    private const string ApiKeyPrefijo = "0123456789ab";
    private const string CuitEmisor = "20123456786";
    private const string CuitOtroContribuyente = "20111111112";

    private static readonly DateTime Octubre = new(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime InicioOctubre = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime FinOctubre = new(2026, 10, 31, 23, 59, 59, DateTimeKind.Utc);
    private static readonly DateTime InicioNoviembre = new(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime FinSeptiembre = new(2026, 9, 30, 23, 59, 59, DateTimeKind.Utc);

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
            Nombre = "Dashboard",
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
    public async Task CP15_Dashboard_SoloCuentaLosComprobantesDelContribuyenteAutenticado()
    {
        await LimpiarAsync();
        await SembrarAsync(
            NuevoComprobante(_contribuyenteA!.Id, TipoComprobante.FacturaC, 1, Octubre, importeTotal: 1000m),
            NuevoComprobante(_contribuyenteB!.Id, TipoComprobante.FacturaC, 1, Octubre.AddHours(1), importeTotal: 9999m));

        var dashboard = await GetDashboardAsync("?mes=2026-10");

        Assert.Equal(1, dashboard.CantidadComprobantes);
        Assert.Equal(1000m, dashboard.ImporteTotal);
    }

    [MongoFact]
    public async Task Dashboard_SoloCuentaLosComprobantesEmitidos()
    {
        await LimpiarAsync();
        await SembrarAsync(
            NuevoComprobante(_contribuyenteA!.Id, TipoComprobante.FacturaC, 1, Octubre, importeTotal: 1000m),
            NuevoComprobante(
                _contribuyenteA.Id,
                TipoComprobante.FacturaC,
                2,
                Octubre.AddHours(1),
                estado: EstadoComprobante.Anulado,
                importeTotal: 500m),
            NuevoComprobante(
                _contribuyenteA.Id,
                TipoComprobante.FacturaC,
                3,
                Octubre.AddHours(2),
                estado: EstadoComprobante.Rechazado,
                importeTotal: 300m),
            NuevoComprobante(
                _contribuyenteA.Id,
                TipoComprobante.FacturaC,
                4,
                Octubre.AddHours(3),
                estado: EstadoComprobante.Borrador,
                importeTotal: 200m));

        var dashboard = await GetDashboardAsync("?mes=2026-10");

        Assert.Equal(1, dashboard.CantidadComprobantes);
        Assert.Equal(1000m, dashboard.ImporteTotal);
    }

    [MongoFact]
    public async Task CP16_Dashboard_ConMesOctubre_RespetaLosBordesDelPeriodoUtc()
    {
        await LimpiarAsync();
        await SembrarAsync(
            NuevoComprobante(_contribuyenteA!.Id, TipoComprobante.FacturaC, 1, FinSeptiembre, importeTotal: 400m),
            NuevoComprobante(_contribuyenteA.Id, TipoComprobante.FacturaC, 2, InicioOctubre, importeTotal: 100m),
            NuevoComprobante(_contribuyenteA.Id, TipoComprobante.FacturaC, 3, FinOctubre, importeTotal: 200m),
            NuevoComprobante(_contribuyenteA.Id, TipoComprobante.FacturaC, 4, InicioNoviembre, importeTotal: 300m));

        var dashboard = await GetDashboardAsync("?mes=2026-10");

        Assert.Equal(InicioOctubre, dashboard.Periodo.Desde);
        Assert.Equal(InicioNoviembre, dashboard.Periodo.Hasta);
        Assert.Equal(2, dashboard.CantidadComprobantes);
        Assert.Equal(300m, dashboard.ImporteTotal);
    }

    [MongoFact]
    public async Task Dashboard_ElImporteTotalDecimal128ConservaLosDosDecimales()
    {
        await LimpiarAsync();
        await SembrarAsync(
            NuevoComprobante(_contribuyenteA!.Id, TipoComprobante.FacturaC, 1, Octubre, importeTotal: 100.10m),
            NuevoComprobante(_contribuyenteA.Id, TipoComprobante.FacturaC, 2, Octubre.AddHours(1), importeTotal: 200.20m));

        var dashboard = await GetDashboardAsync("?mes=2026-10");

        Assert.Equal(2, dashboard.CantidadComprobantes);
        Assert.Equal(300.30m, dashboard.ImporteTotal);
    }

    [MongoFact]
    public async Task Dashboard_UltimoCaeYUltimosComprobantes_UsanLasFechasMasRecientes()
    {
        await LimpiarAsync();
        await SembrarAsync(
            NuevoComprobante(_contribuyenteA!.Id, TipoComprobante.FacturaC, 1, new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc), cae: "11111111111111"),
            NuevoComprobante(_contribuyenteA.Id, TipoComprobante.FacturaC, 2, new DateTime(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc), cae: "22222222222222"),
            NuevoComprobante(_contribuyenteA.Id, TipoComprobante.FacturaC, 3, new DateTime(2026, 11, 25, 12, 0, 0, DateTimeKind.Utc), estado: EstadoComprobante.Anulado, cae: "33333333333333"),
            NuevoComprobante(_contribuyenteA.Id, TipoComprobante.FacturaC, 4, new DateTime(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc)),
            NuevoComprobante(_contribuyenteA.Id, TipoComprobante.FacturaC, 5, new DateTime(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc)),
            NuevoComprobante(_contribuyenteA.Id, TipoComprobante.FacturaC, 6, new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc)),
            NuevoComprobante(_contribuyenteA.Id, TipoComprobante.FacturaC, 7, new DateTime(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc)));

        var dashboard = await GetDashboardAsync("?mes=2026-10");

        Assert.Equal("22222222222222", dashboard.UltimoCae);
        Assert.Equal(5, dashboard.UltimosComprobantes.Count);
        Assert.Equal([3L, 2L, 7L, 6L, 5L], dashboard.UltimosComprobantes.Select(c => c.Numero).ToArray());
        Assert.Equal("33333333333333", dashboard.UltimosComprobantes[0].Cae);
    }

    [MongoFact]
    public async Task Dashboard_UltimoNumeroAutorizado_EsElMaximoEntreSecuenciasDeDistintoTipoYPunto()
    {
        await LimpiarAsync();
        await Secuencias().InsertManyAsync(
        [
            new Secuencia
            {
                ContribuyenteId = _contribuyenteA!.Id,
                PuntoVenta = 1,
                TipoComprobante = TipoComprobante.FacturaC,
                UltimoNumero = 42
            },
            new Secuencia
            {
                ContribuyenteId = _contribuyenteA.Id,
                PuntoVenta = 2,
                TipoComprobante = TipoComprobante.FacturaA,
                UltimoNumero = 77
            },
            new Secuencia
            {
                ContribuyenteId = _contribuyenteA.Id,
                PuntoVenta = 1,
                TipoComprobante = TipoComprobante.NotaCreditoC,
                UltimoNumero = 5
            }
        ]);

        var dashboard = await GetDashboardAsync("?mes=2026-10");

        Assert.Equal(77, dashboard.UltimoNumeroAutorizado);
    }

    [MongoFact]
    public async Task Dashboard_SinComprobantes_DevuelveEstadoVacio()
    {
        await LimpiarAsync();

        var dashboard = await GetDashboardAsync("?mes=2026-10");

        Assert.Equal(0, dashboard.CantidadComprobantes);
        Assert.Equal(0m, dashboard.ImporteTotal);
        Assert.Null(dashboard.UltimoCae);
        Assert.Equal(0, dashboard.UltimoNumeroAutorizado);
        Assert.Empty(dashboard.UltimosComprobantes);
    }

    private WebApplicationFactory<Program> CrearFactory()
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
            });

    private static HttpClient CrearCliente(WebApplicationFactory<Program> factory)
        => factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false,
            AllowAutoRedirect = false
        });

    private async Task<DashboardResponse> GetDashboardAsync(string query)
    {
        using var mensaje = new HttpRequestMessage(HttpMethod.Get, $"/v1/comprobantes/dashboard{query}");
        mensaje.Headers.TryAddWithoutValidation("X-Api-Key", ApiKeyValor);

        var response = await Client.SendAsync(mensaje);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await LeerAsync<DashboardResponse>(response);
    }

    private async Task LimpiarAsync()
    {
        await Comprobantes().DeleteManyAsync(FilterDefinition<Comprobante>.Empty);
        await Secuencias().DeleteManyAsync(FilterDefinition<Secuencia>.Empty);
        await Eventos().DeleteManyAsync(FilterDefinition<EventoAuditoria>.Empty);
    }

    private Task SembrarAsync(params Comprobante[] comprobantes)
        => Comprobantes().InsertManyAsync(comprobantes);

    private Comprobante NuevoComprobante(
        Guid contribuyenteId,
        TipoComprobante tipo,
        long numero,
        DateTime fecha,
        int puntoVenta = 1,
        EstadoComprobante estado = EstadoComprobante.Emitido,
        decimal importeTotal = 12100m,
        string? cae = null)
        => new()
        {
            ContribuyenteId = contribuyenteId,
            EmisorCuit = CuitEmisor,
            EmisorRazonSocial = "Demo SRL",
            PuntoVenta = puntoVenta,
            TipoComprobante = tipo,
            Numero = numero,
            FechaEmision = fecha,
            Cae = cae ?? Interlocked.Increment(ref _caeContador).ToString("D14", CultureInfo.InvariantCulture),
            CaeVencimiento = fecha.Date.AddDays(10),
            ReceptorDocumentoTipo = "CUIT",
            ReceptorDocumentoNumero = "20345678901",
            ReceptorRazonSocial = "Cliente S.A.",
            ReceptorCondicionIva = CondicionIva.ResponsableInscripto,
            ImporteNeto = importeTotal,
            ImporteIva = 0m,
            ImporteTotal = importeTotal,
            Moneda = "ARS",
            Estado = estado,
            Items = []
        };

    private static async Task<T> LeerAsync<T>(HttpResponseMessage response)
        => (await response.Content.ReadFromJsonAsync<T>())
            ?? throw new InvalidOperationException($"La respuesta no contiene {typeof(T).Name}.");

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
