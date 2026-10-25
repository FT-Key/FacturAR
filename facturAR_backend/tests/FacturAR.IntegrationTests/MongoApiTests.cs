using System.Net;
using System.Net.Http.Json;
using FacturAR.Contracts.Comprobantes;
using FacturAR.Domain.Entities;
using FacturAR.Domain.Enums;
using FacturAR.Infrastructure.Mongo;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace FacturAR.IntegrationTests;

public sealed class MongoApiTests : IAsyncLifetime
{
    private const string ExistingCae = "99999999999998";
    private const string MissingCae = "99999999999997";

    private readonly string _databaseName = $"facturar_it_{Guid.NewGuid():N}"[..28];
    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;
    private IMongoClient? _mongoClient;

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
            });

        _client = _factory.CreateClient();
        _mongoClient = _factory.Services.GetRequiredService<IMongoClient>();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var mongoContext = _factory.Services.GetRequiredService<MongoDbContext>();
        await MongoIndexInitializer.InitializeAsync(mongoContext, timeout.Token);
    }

    [MongoFact]
    public async Task Verificar_ConComprobanteEnMongo_DevuelveDatosPublicosSinCamposInternos()
    {
        var comprobante = BuildComprobante();
        await GetComprobantes().InsertOneAsync(comprobante);

        var response = await Client.GetAsync($"/v1/comprobantes/verificar/{ExistingCae}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<VerificacionResponse>();
        Assert.NotNull(payload);
        Assert.Equal(ExistingCae, payload.Cae);
        Assert.Equal(comprobante.EmisorCuit, payload.CuitEmisor);
        Assert.Equal(comprobante.EmisorRazonSocial, payload.RazonSocialEmisor);
        Assert.Equal((int)TipoComprobante.FacturaA, payload.TipoComprobante);
        Assert.Equal("Factura A", payload.TipoComprobanteDescripcion);
        Assert.Equal(comprobante.PuntoVenta, payload.PuntoVenta);
        Assert.Equal(comprobante.Numero, payload.Numero);
        Assert.Equal(comprobante.ImporteTotal, payload.ImporteTotal);
        Assert.Equal("ARS", payload.Moneda);
        Assert.Equal("Emitido", payload.Estado);

        var rawPayload = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("idempotencyKey", rawPayload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("contribuyenteId", rawPayload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("receptorDocumentoNumero", rawPayload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("observaciones", rawPayload, StringComparison.OrdinalIgnoreCase);
    }

    [MongoFact]
    public async Task Verificar_ConCaeInexistente_DevuelveNotFound()
    {
        var response = await Client.GetAsync($"/v1/comprobantes/verificar/{MissingCae}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(payload);
        Assert.Equal("COMPROBANTE_NO_ENCONTRADO", payload.Codigo);
    }

    [MongoFact]
    public async Task InicializarIndices_CreaElIndiceUnicoDeCae()
    {
        var indexes = await (await GetComprobantes().Indexes.ListAsync()).ToListAsync();

        Assert.Contains(indexes, index =>
            index.TryGetValue("name", out var name)
            && name.IsString
            && name.AsString == "ux_comprobantes_cae"
            && index.TryGetValue("unique", out var unique)
            && unique.IsBoolean
            && unique.AsBoolean);
    }

    public async Task DisposeAsync()
    {
        if (_mongoClient is not null)
        {
            await _mongoClient.DropDatabaseAsync(_databaseName);
        }

        _client?.Dispose();
        _factory?.Dispose();
    }

    private HttpClient Client => _client
        ?? throw new InvalidOperationException("El test de MongoDB no fue inicializado.");

    private IMongoCollection<Comprobante> GetComprobantes() => (_mongoClient
        ?? throw new InvalidOperationException("El test de MongoDB no fue inicializado."))
        .GetDatabase(_databaseName)
        .GetCollection<Comprobante>(MongoCollections.Comprobantes);

    private static Comprobante BuildComprobante() => new()
    {
        ContribuyenteId = Guid.NewGuid(),
        EmisorCuit = "20123456786",
        EmisorRazonSocial = "Empresa de Prueba S.A.",
        PuntoVenta = 1,
        TipoComprobante = TipoComprobante.FacturaA,
        Numero = 42,
        FechaEmision = new DateTime(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc),
        Cae = ExistingCae,
        CaeVencimiento = new DateTime(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc),
        ReceptorDocumentoTipo = "80",
        ReceptorDocumentoNumero = "30712345678",
        ReceptorRazonSocial = "Cliente de Prueba S.A.",
        ReceptorCondicionIva = CondicionIva.ResponsableInscripto,
        ImporteNeto = 100m,
        ImporteIva = 21m,
        ImporteTotal = 121m,
        Moneda = "ARS",
        Estado = EstadoComprobante.Emitido,
        Observaciones = "No debe exponerse en la verificación pública."
    };

    private sealed record ErrorResponse(string Codigo, string Mensaje);
}
