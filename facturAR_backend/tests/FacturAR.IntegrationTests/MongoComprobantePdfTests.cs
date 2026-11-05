using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text;
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

public sealed class MongoComprobantePdfTests : IAsyncLifetime
{
    private const string ApiKeyValor = "fak_0123456789ab_GGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGG";
    private const string ApiKeyPrefijo = "0123456789ab";
    private const string CuitEmisor = "20123456786";
    private const string CuitReceptor = "30712345671";
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

        await PuntosVenta().InsertOneAsync(new PuntoVenta
        {
            ContribuyenteId = _contribuyenteA.Id,
            Numero = 1,
            Descripcion = "Principal",
            Activo = true
        });

        await ApiKeys().InsertOneAsync(new ApiKey
        {
            ContribuyenteId = _contribuyenteA.Id,
            Nombre = "PDF",
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
    public async Task CP01_PdfDeUnComprobantePropio_Devuelve200ConHeadersYFirmaPdf()
    {
        var comprobante = await EmitirFacturaAAsync("idem-pdf-cp01");

        var response = await GetPdfAsync(comprobante.Id);

        await AssertPdfAsync(response, "0001-00000001.pdf");
    }

    [MongoFact]
    public async Task CP05_PdfDeUnComprobanteDeOtraEmpresa_Devuelve404SinFiltrarDatos()
    {
        var ajeno = NuevoComprobanteAjeno();
        await Comprobantes().InsertOneAsync(ajeno);

        var response = await GetPdfAsync(ajeno.Id);

        await AssertErrorAsync(response, HttpStatusCode.NotFound, "COMPROBANTE_NO_ENCONTRADO");

        var cuerpo = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Otra SRL", cuerpo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(ajeno.Cae, cuerpo, StringComparison.Ordinal);
        Assert.DoesNotContain("%PDF", cuerpo, StringComparison.Ordinal);
    }

    [MongoFact]
    public async Task CP06_PdfDeUnIdInexistente_Devuelve404ComprobanteNoEncontrado()
    {
        var response = await GetPdfAsync(Guid.NewGuid());

        await AssertErrorAsync(response, HttpStatusCode.NotFound, "COMPROBANTE_NO_ENCONTRADO");
    }

    [MongoFact]
    public async Task CP07_RegenerarElPdfNoAlteraElComprobanteNiConsumeNumero()
    {
        var comprobante = await EmitirFacturaAAsync("idem-pdf-cp07");
        var antes = await Comprobantes().Find(c => c.Id == comprobante.Id).SingleAsync();

        var primera = await GetPdfAsync(comprobante.Id);
        var segunda = await GetPdfAsync(comprobante.Id);

        await AssertPdfAsync(primera, "0001-00000001.pdf");
        await AssertPdfAsync(segunda, "0001-00000001.pdf");

        var despues = await Comprobantes().Find(c => c.Id == comprobante.Id).SingleAsync();
        Assert.Equal(antes.Numero, despues.Numero);
        Assert.Equal(antes.Cae, despues.Cae);
        Assert.Equal(antes.CreatedAt, despues.CreatedAt);
        Assert.Equal(antes.ImporteNeto, despues.ImporteNeto);
        Assert.Equal(antes.ImporteIva, despues.ImporteIva);
        Assert.Equal(antes.ImporteTotal, despues.ImporteTotal);
        Assert.Equal(antes.Items.Count, despues.Items.Count);
        Assert.Equal(
            antes.Items.Select(item => item.Subtotal).ToArray(),
            despues.Items.Select(item => item.Subtotal).ToArray());

        var documento = await Comprobantes()
            .Find(c => c.Id == comprobante.Id)
            .Project<BsonDocument>(Builders<Comprobante>.Projection.Exclude("_id"))
            .SingleAsync();
        Assert.False(documento.Contains("UpdatedAt"));

        Assert.Equal(1, await Comprobantes().CountDocumentsAsync(FilterDefinition<Comprobante>.Empty));
        var secuencia = await SecuenciaAsync(TipoComprobante.FacturaA);
        Assert.Equal(1, secuencia.UltimoNumero);
    }

    [MongoFact]
    public async Task CP08_GenerarElPdfConWarmUp_CompletaEnMenosDeTresSegundos()
    {
        var comprobante = await EmitirFacturaAAsync("idem-pdf-cp08");

        var calentamiento = await GetPdfAsync(comprobante.Id);
        await AssertPdfAsync(calentamiento, "0001-00000001.pdf");

        var cronometro = Stopwatch.StartNew();
        var response = await GetPdfAsync(comprobante.Id);
        cronometro.Stop();

        await AssertPdfAsync(response, "0001-00000001.pdf");
        Assert.True(
            cronometro.Elapsed < TimeSpan.FromSeconds(3),
            $"La generación tardó {cronometro.Elapsed.TotalMilliseconds:N0} ms.");
    }

    private async Task<ComprobanteResponse> EmitirFacturaAAsync(string idempotencyKey)
    {
        var request = new EmitirComprobanteRequest(
            (int)TipoComprobante.FacturaA,
            1,
            FechaEmision,
            new ReceptorComprobanteRequest(
                "CUIT",
                CuitReceptor,
                "Cliente S.A.",
                (int)CondicionIva.ResponsableInscripto),
            [new ItemComprobanteRequest("Servicio", 2m, 5000m, 21m)],
            12100m,
            null);

        using var mensaje = new HttpRequestMessage(HttpMethod.Post, "/v1/comprobantes")
        {
            Content = JsonContent.Create(request)
        };
        mensaje.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        mensaje.Headers.TryAddWithoutValidation("X-Api-Key", ApiKeyValor);

        var response = await Client.SendAsync(mensaje);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await LeerAsync<ComprobanteResponse>(response);
    }

    private async Task<HttpResponseMessage> GetPdfAsync(Guid id)
    {
        using var mensaje = new HttpRequestMessage(HttpMethod.Get, $"/v1/comprobantes/{id}/pdf");
        mensaje.Headers.TryAddWithoutValidation("X-Api-Key", ApiKeyValor);

        return await Client.SendAsync(mensaje);
    }

    private static async Task AssertPdfAsync(HttpResponseMessage response, string nombreArchivo)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(
            $"inline; filename=\"{nombreArchivo}\"",
            response.Content.Headers.GetValues("Content-Disposition").Single());

        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 1000);
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(bytes, 0, 5));
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

    private Comprobante NuevoComprobanteAjeno()
        => new()
        {
            ContribuyenteId = _contribuyenteB!.Id,
            EmisorCuit = CuitOtroContribuyente,
            EmisorRazonSocial = "Otra SRL",
            PuntoVenta = 1,
            TipoComprobante = TipoComprobante.FacturaA,
            Numero = 7,
            FechaEmision = FechaEmision,
            Cae = "99999999999999",
            CaeVencimiento = FechaEmision.Date.AddDays(10),
            ReceptorDocumentoTipo = "CUIT",
            ReceptorDocumentoNumero = "20345678901",
            ReceptorRazonSocial = "Cliente Ajeno S.A.",
            ReceptorCondicionIva = CondicionIva.ResponsableInscripto,
            ImporteNeto = 1000m,
            ImporteIva = 210m,
            ImporteTotal = 1210m,
            Moneda = "ARS",
            Estado = EstadoComprobante.Emitido,
            IdempotencyKey = "idem-ajeno-pdf",
            Items =
            [
                new ComprobanteItem
                {
                    Descripcion = "Servicio ajeno",
                    Cantidad = 1m,
                    PrecioUnitario = 1000m,
                    AlicuotaIva = 21m,
                    Subtotal = 1000m
                }
            ]
        };

    private Task<Secuencia> SecuenciaAsync(TipoComprobante tipo)
    {
        var filtro = Builders<Secuencia>.Filter.Eq(s => s.ContribuyenteId, _contribuyenteA!.Id)
            & Builders<Secuencia>.Filter.Eq(s => s.PuntoVenta, 1)
            & Builders<Secuencia>.Filter.Eq(s => s.TipoComprobante, tipo);

        return Secuencias().Find(filtro).SingleAsync();
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

    private IMongoCollection<T> Coleccion<T>(string nombre)
        => _mongoClient!.GetDatabase(_databaseName).GetCollection<T>(nombre);
}
