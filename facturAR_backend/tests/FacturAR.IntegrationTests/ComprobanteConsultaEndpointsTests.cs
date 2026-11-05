using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FacturAR.Application.Abstractions;
using FacturAR.Application.ApiKeys;
using FacturAR.Application.Comprobantes;
using FacturAR.Contracts.Auth;
using FacturAR.Contracts.Comunes;
using FacturAR.Contracts.Comprobantes;
using FacturAR.Domain.Entities;
using FacturAR.Domain.Enums;
using FacturAR.Infrastructure.Password;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FacturAR.IntegrationTests;

public sealed class ComprobanteConsultaEndpointsTests
{
    public const string ApiKeyValida = "fak_0123456789ab_FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF";
    private const string IdDetalleInexistente = "9f3f5f70-1c3e-4f0a-9f3e-9d2f0b7a1c22";

    [Theory]
    [InlineData("/v1/comprobantes")]
    [InlineData("/v1/comprobantes/9f3f5f70-1c3e-4f0a-9f3e-9d2f0b7a1c22")]
    [InlineData("/v1/comprobantes/ultimo-autorizado?tipo=11")]
    public async Task Consultas_SinApiKeyNiCookie_Devuelven401ApiKeyAusente(string ruta)
    {
        using var factory = new ComprobanteConsultaFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(ruta);

        await AssertErrorAsync(response, HttpStatusCode.Unauthorized, "API_KEY_AUSENTE");
        Assert.Equal(0, factory.Comprobantes.ConsultasListado);
        Assert.Equal(0, factory.Comprobantes.ConsultasDetalle);
        Assert.Equal(0, factory.Secuencias.Consultas);
    }

    [Theory]
    [InlineData("/v1/comprobantes/not-a-guid")]
    [InlineData("/v1/comprobantes/123")]
    public async Task Detalle_ConIdQueNoEsGuid_Devuelve400ValidacionSinConsultarElRepositorio(string ruta)
    {
        using var factory = new ComprobanteConsultaFactory();
        using var client = factory.CreateClient();

        var response = await EnviarConApiKeyAsync(client, ruta);

        await AssertErrorAsync(response, HttpStatusCode.BadRequest, "VALIDACION");
        Assert.Equal(0, factory.Comprobantes.ConsultasDetalle);
    }

    [Fact]
    public async Task Listar_ConPageSizeQuinientos_Devuelve400Validacion()
    {
        using var factory = new ComprobanteConsultaFactory();
        using var client = factory.CreateClient();

        var response = await EnviarConApiKeyAsync(client, "/v1/comprobantes?pageSize=500");

        await AssertErrorAsync(response, HttpStatusCode.BadRequest, "VALIDACION");
        Assert.Equal(0, factory.Comprobantes.ConsultasListado);
    }

    [Fact]
    public async Task Listar_ConPageSizeCien_Devuelve200()
    {
        using var factory = new ComprobanteConsultaFactory();
        using var client = factory.CreateClient();

        var response = await EnviarConApiKeyAsync(client, "/v1/comprobantes?pageSize=100");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var pagina = await LeerAsync<PaginaResponse<ComprobanteResumenResponse>>(response);
        Assert.Equal(100, pagina.PageSize);
        Assert.Equal(1, pagina.Page);
    }

    [Theory]
    [InlineData("/v1/comprobantes?tipo=99")]
    [InlineData("/v1/comprobantes?tipo=0")]
    [InlineData("/v1/comprobantes?estado=9")]
    [InlineData("/v1/comprobantes?estado=-1")]
    [InlineData("/v1/comprobantes?desde=2026-12-01&hasta=2026-11-01")]
    [InlineData("/v1/comprobantes?desde=no-es-fecha")]
    [InlineData("/v1/comprobantes?puntoVenta=0")]
    public async Task Listar_ConFiltroInvalido_Devuelve400Validacion(string ruta)
    {
        using var factory = new ComprobanteConsultaFactory();
        using var client = factory.CreateClient();

        var response = await EnviarConApiKeyAsync(client, ruta);

        await AssertErrorAsync(response, HttpStatusCode.BadRequest, "VALIDACION");
        Assert.Equal(0, factory.Comprobantes.ConsultasListado);
    }

    [Fact]
    public async Task Detalle_ConIdInexistente_Devuelve404ComprobanteNoEncontrado()
    {
        using var factory = new ComprobanteConsultaFactory();
        using var client = factory.CreateClient();

        var response = await EnviarConApiKeyAsync(client, $"/v1/comprobantes/{IdDetalleInexistente}");

        await AssertErrorAsync(response, HttpStatusCode.NotFound, "COMPROBANTE_NO_ENCONTRADO");
        Assert.Equal(1, factory.Comprobantes.ConsultasDetalle);
    }

    [Fact]
    public async Task Detalle_DeOtroContribuyente_Devuelve404SinRevelarElRecurso()
    {
        using var factory = new ComprobanteConsultaFactory();
        var ajeno = NuevoComprobante();
        ajeno.Id = Guid.NewGuid();
        ajeno.ContribuyenteId = Guid.NewGuid();
        factory.Comprobantes.Comprobantes.Add(ajeno);
        using var client = factory.CreateClient();

        var response = await EnviarConApiKeyAsync(client, $"/v1/comprobantes/{ajeno.Id}");

        await AssertErrorAsync(response, HttpStatusCode.NotFound, "COMPROBANTE_NO_ENCONTRADO");
        Assert.Equal(1, factory.Comprobantes.ConsultasDetalle);
        Assert.Equal(ComprobanteConsultaFactory.ContribuyenteId, factory.Comprobantes.UltimoContribuyenteId);
    }

    [Fact]
    public async Task Listar_NoIncluyeComprobantesDeOtroContribuyente()
    {
        using var factory = new ComprobanteConsultaFactory();
        factory.Comprobantes.Comprobantes.Add(NuevoComprobante());
        var ajeno = NuevoComprobante();
        ajeno.Id = Guid.NewGuid();
        ajeno.ContribuyenteId = Guid.NewGuid();
        factory.Comprobantes.Comprobantes.Add(ajeno);
        using var client = factory.CreateClient();

        var response = await EnviarConApiKeyAsync(client, "/v1/comprobantes");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var pagina = await LeerAsync<PaginaResponse<ComprobanteResumenResponse>>(response);
        Assert.Equal(1, pagina.Total);
        Assert.Single(pagina.Items);
        Assert.Equal(ComprobanteConsultaFactory.ContribuyenteId, factory.Comprobantes.UltimoContribuyenteId);
    }

    [Fact]
    public async Task Listar_ConFiltroDeTipoYPageSize_MantieneElFiltroYPaginacion()
    {
        using var factory = new ComprobanteConsultaFactory();
        factory.Comprobantes.Comprobantes.Add(NuevoComprobante());
        var facturaA = NuevoComprobante();
        facturaA.Id = Guid.NewGuid();
        facturaA.TipoComprobante = TipoComprobante.FacturaA;
        facturaA.Numero = 43;
        factory.Comprobantes.Comprobantes.Add(facturaA);
        using var client = factory.CreateClient();

        var response = await EnviarConApiKeyAsync(
            client,
            "/v1/comprobantes?tipo=11&page=1&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var pagina = await LeerAsync<PaginaResponse<ComprobanteResumenResponse>>(response);
        Assert.Equal(1, pagina.Total);
        Assert.Equal(1, pagina.Page);
        Assert.Equal(10, pagina.PageSize);
        var item = Assert.Single(pagina.Items);
        Assert.Equal((int)TipoComprobante.FacturaC, item.TipoComprobante);
    }

    [Fact]
    public async Task Listar_OrdenaPorFechaYNumeroDescendente()
    {
        using var factory = new ComprobanteConsultaFactory();
        var viejo = NuevoComprobante();
        viejo.FechaEmision = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
        viejo.Numero = 1;
        var nuevo = NuevoComprobante();
        nuevo.Id = Guid.NewGuid();
        nuevo.FechaEmision = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);
        nuevo.Numero = 2;
        factory.Comprobantes.Comprobantes.Add(viejo);
        factory.Comprobantes.Comprobantes.Add(nuevo);
        using var client = factory.CreateClient();

        var response = await EnviarConApiKeyAsync(client, "/v1/comprobantes");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var pagina = await LeerAsync<PaginaResponse<ComprobanteResumenResponse>>(response);
        Assert.Equal([2L, 1L], pagina.Items.Select(i => i.Numero).ToArray());
    }

    [Fact]
    public async Task Listar_ConIdempotencyKey_NoRepiteNiAfectaLaConsulta()
    {
        using var factory = new ComprobanteConsultaFactory();
        factory.Comprobantes.Comprobantes.Add(NuevoComprobante());
        using var client = factory.CreateClient();

        using var mensaje = new HttpRequestMessage(HttpMethod.Get, "/v1/comprobantes");
        mensaje.Headers.TryAddWithoutValidation("X-Api-Key", ApiKeyValida);
        mensaje.Headers.TryAddWithoutValidation("Idempotency-Key", "clave-solo-para-emision");

        var response = await client.SendAsync(mensaje);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var pagina = await LeerAsync<PaginaResponse<ComprobanteResumenResponse>>(response);
        Assert.Equal(1, pagina.Total);
        Assert.Equal(1, factory.Comprobantes.ConsultasListado);
        Assert.Equal(0, factory.Comprobantes.ConsultasDetalle);
    }

    [Fact]
    public async Task Listar_ConApiKey_UsaElContribuyenteDelTokenYDevuelveElResumen()
    {
        using var factory = new ComprobanteConsultaFactory();
        factory.Comprobantes.Comprobantes.Add(NuevoComprobante());
        using var client = factory.CreateClient();

        var response = await EnviarConApiKeyAsync(client, "/v1/comprobantes");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var pagina = await LeerAsync<PaginaResponse<ComprobanteResumenResponse>>(response);
        Assert.Equal(ComprobanteConsultaFactory.ContribuyenteId, factory.Comprobantes.UltimoContribuyenteId);
        Assert.Equal(1, pagina.Total);
        Assert.Single(pagina.Items);
    }

    [Fact]
    public async Task Respuestas_NoExponenCamposInternosNiImportesFormateados()
    {
        using var factory = new ComprobanteConsultaFactory();
        var comprobante = NuevoComprobante();
        factory.Comprobantes.Comprobantes.Add(comprobante);
        using var client = factory.CreateClient();

        var listado = await EnviarConApiKeyAsync(client, "/v1/comprobantes");
        var detalle = await EnviarConApiKeyAsync(client, $"/v1/comprobantes/{comprobante.Id}");

        Assert.Equal(HttpStatusCode.OK, listado.StatusCode);
        Assert.Equal(HttpStatusCode.OK, detalle.StatusCode);

        var jsonListado = await listado.Content.ReadAsStringAsync();
        var jsonDetalle = await detalle.Content.ReadAsStringAsync();

        AssertNoExponeCamposInternos(jsonListado);
        AssertNoExponeCamposInternos(jsonDetalle);

        using var documentoListado = JsonDocument.Parse(jsonListado);
        var item = documentoListado.RootElement.GetProperty("items")[0];
        Assert.Equal(JsonValueKind.Number, item.GetProperty("importeTotal").ValueKind);

        using var documentoDetalle = JsonDocument.Parse(jsonDetalle);
        Assert.Equal(JsonValueKind.Number, documentoDetalle.RootElement.GetProperty("importeTotal").ValueKind);
        Assert.Equal(JsonValueKind.Number, documentoDetalle.RootElement.GetProperty("importeNeto").ValueKind);
    }

    [Fact]
    public async Task Listar_ConCookieDeEmpresaRolConsulta_Devuelve200()
    {
        using var factory = new ComprobanteConsultaFactory();
        factory.Comprobantes.Comprobantes.Add(NuevoComprobante());
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false,
            AllowAutoRedirect = false
        });

        var login = await client.PostAsJsonAsync(
            "/v1/auth/login",
            new LoginRequest(ComprobanteConsultaFactory.EmailUsuario, ComprobanteConsultaFactory.PasswordUsuario));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var cookie = login.Headers.GetValues("Set-Cookie").First().Split(';')[0];

        using var mensaje = new HttpRequestMessage(HttpMethod.Get, "/v1/comprobantes");
        mensaje.Headers.TryAddWithoutValidation("Cookie", cookie);

        var response = await client.SendAsync(mensaje);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var pagina = await LeerAsync<PaginaResponse<ComprobanteResumenResponse>>(response);
        Assert.Equal(1, pagina.Total);
        Assert.Equal(ComprobanteConsultaFactory.ContribuyenteId, factory.Comprobantes.UltimoContribuyenteId);
    }

    private static Comprobante NuevoComprobante()
        => new()
        {
            ContribuyenteId = ComprobanteConsultaFactory.ContribuyenteId,
            EmisorCuit = ComprobanteConsultaFactory.CuitEmisor,
            EmisorRazonSocial = "Demo SRL",
            PuntoVenta = 1,
            TipoComprobante = TipoComprobante.FacturaC,
            Numero = 42,
            FechaEmision = new DateTime(2026, 11, 30, 15, 0, 0, DateTimeKind.Utc),
            Cae = "12345678901234",
            CaeVencimiento = new DateTime(2026, 12, 10, 0, 0, 0, DateTimeKind.Utc),
            ReceptorDocumentoTipo = "CUIT",
            ReceptorDocumentoNumero = "20345678901",
            ReceptorRazonSocial = "Cliente Final",
            ReceptorCondicionIva = CondicionIva.ResponsableInscripto,
            ImporteNeto = 10000m,
            ImporteIva = 2100m,
            ImporteTotal = 12100m,
            Moneda = "ARS",
            Estado = EstadoComprobante.Emitido,
            IdempotencyKey = "idem-interna-no-expuesta",
            Items =
            [
                new ComprobanteItem
                {
                    Descripcion = "Servicio",
                    Cantidad = 1m,
                    PrecioUnitario = 10000m,
                    AlicuotaIva = 21m,
                    Subtotal = 10000m
                }
            ]
        };

    private static void AssertNoExponeCamposInternos(string json)
    {
        Assert.DoesNotContain("contribuyenteId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("idempotencyKey", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hash", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("apikey", json, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<HttpResponseMessage> EnviarConApiKeyAsync(HttpClient client, string ruta)
    {
        using var mensaje = new HttpRequestMessage(HttpMethod.Get, ruta);
        mensaje.Headers.TryAddWithoutValidation("X-Api-Key", ApiKeyValida);

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
}

public sealed class ComprobanteConsultaFactory : WebApplicationFactory<Program>
{
    public const string EmailUsuario = "consulta@demo.com";
    public const string PasswordUsuario = "Secreta123!";
    public const string CuitEmisor = "20123456786";
    public const string CuitReceptor = "20345678901";

    public static readonly Guid ContribuyenteId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    public static readonly Guid UsuarioId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    public static readonly Guid ApiKeyId = Guid.Parse("66666666-6666-6666-6666-666666666666");

    private readonly PasswordHasher _hasher = new(1000);

    public ComprobanteConsultaFactory()
    {
        Contribuyentes.Contribuyentes.Add(new Contribuyente
        {
            Id = ContribuyenteId,
            RazonSocial = "Demo SRL",
            Cuit = CuitEmisor,
            CondicionIva = CondicionIva.ResponsableInscripto,
            Activo = true
        });
        Usuarios.Usuario = new Usuario
        {
            Id = UsuarioId,
            ContribuyenteId = ContribuyenteId,
            Email = EmailUsuario,
            PasswordHash = _hasher.Hash(PasswordUsuario),
            Rol = RolUsuario.Consulta,
            Activo = true
        };
    }

    public ComprobanteContribuyenteFake Contribuyentes { get; } = new();

    public ComprobantePuntoVentaFake PuntosVenta { get; } = new();

    public ComprobanteParametrosFake Parametros { get; } = new();

    public ComprobanteUsuarioFake Usuarios { get; } = new();

    public ComprobanteEventoFake Eventos { get; } = new();

    public ConsultaComprobantesRepositorioFake Comprobantes { get; } = new();

    public ConsultaSecuenciasRepositorioFake Secuencias { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Mongo:InitializeIndexes", "false");
        builder.UseSetting("ApiKeys:ActualizarUltimoUso", "false");
        builder.UseSetting("Seed:Enabled", "false");
        builder.UseSetting("BootstrapAdmin:Email", string.Empty);
        builder.UseSetting("Auth:PasswordHashIterations", "1000");
        builder.UseSetting("Auth:Login:RateLimitPorIp", "1000");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IApiKeyAuthenticator>();
            services.AddSingleton<IApiKeyAuthenticator>(new ComprobanteApiKeyAuthenticatorFake(
                new Dictionary<string, ResultadoApiKey>(StringComparer.Ordinal)
                {
                    [ComprobanteConsultaEndpointsTests.ApiKeyValida] =
                        new ResultadoApiKey(ApiKeyId, ContribuyenteId)
                }));

            services.RemoveAll<IContribuyenteRepository>();
            services.AddSingleton<IContribuyenteRepository>(Contribuyentes);

            services.RemoveAll<IPuntoVentaRepository>();
            services.AddSingleton<IPuntoVentaRepository>(PuntosVenta);

            services.RemoveAll<IParametrosResolver>();
            services.AddSingleton<IParametrosResolver>(Parametros);

            services.RemoveAll<IUsuarioRepository>();
            services.AddSingleton<IUsuarioRepository>(Usuarios);

            services.RemoveAll<IEventoAuditoriaRepository>();
            services.AddSingleton<IEventoAuditoriaRepository>(Eventos);

            services.RemoveAll<IComprobanteRepository>();
            services.AddSingleton<IComprobanteRepository>(Comprobantes);

            services.RemoveAll<ISecuenciaRepository>();
            services.AddSingleton<ISecuenciaRepository>(Secuencias);
        });
    }
}

public sealed class ConsultaComprobantesRepositorioFake : IComprobanteRepository
{
    public List<Comprobante> Comprobantes { get; } = [];

    public Guid? UltimoContribuyenteId { get; private set; }

    public int ConsultasListado { get; private set; }

    public int ConsultasDetalle { get; private set; }

    public int ConsultasMetricas { get; private set; }

    public Guid? UltimoContribuyenteMetricas { get; private set; }

    public DateTime? UltimoDesdeMetricas { get; private set; }

    public DateTime? UltimoHastaMetricas { get; private set; }

    public Exception? ExcepcionMetricas { get; set; }

    public Task<Comprobante?> GetByCaeAsync(
        string cae,
        CancellationToken cancellationToken = default)
        => Task.FromResult(Comprobantes.FirstOrDefault(c => c.Cae == cae));

    public Task<Comprobante?> GetByIdAsync(
        Guid contribuyenteId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        ConsultasDetalle++;
        UltimoContribuyenteId = contribuyenteId;

        return Task.FromResult(Comprobantes.FirstOrDefault(
            c => c.Id == id && c.ContribuyenteId == contribuyenteId));
    }

    public Task<Comprobante?> GetByNumeracionAsync(
        Guid contribuyenteId,
        int puntoVenta,
        TipoComprobante tipo,
        long numero,
        CancellationToken cancellationToken = default)
    {
        ConsultasDetalle++;
        UltimoContribuyenteId = contribuyenteId;

        return Task.FromResult(Comprobantes.FirstOrDefault(
            c => c.ContribuyenteId == contribuyenteId
                && c.PuntoVenta == puntoVenta
                && c.TipoComprobante == tipo
                && c.Numero == numero));
    }

    public Task<Comprobante?> GetUltimoEmitidoAsync(
        Guid contribuyenteId,
        int puntoVenta,
        TipoComprobante tipo,
        CancellationToken cancellationToken = default)
    {
        ConsultasDetalle++;
        UltimoContribuyenteId = contribuyenteId;

        return Task.FromResult(Comprobantes
            .Where(c => c.ContribuyenteId == contribuyenteId
                && c.PuntoVenta == puntoVenta
                && c.TipoComprobante == tipo
                && c.Estado == EstadoComprobante.Emitido)
            .OrderByDescending(c => c.Numero)
            .FirstOrDefault());
    }

    public Task<(IReadOnlyList<Comprobante> Items, long Total)> ListarAsync(
        Guid contribuyenteId,
        FiltroListadoComprobantes filtro,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ConsultasListado++;
        UltimoContribuyenteId = contribuyenteId;

        var coincidencias = Comprobantes
            .Where(c => c.ContribuyenteId == contribuyenteId)
            .Where(c => filtro.Desde is null || c.FechaEmision >= filtro.Desde)
            .Where(c => filtro.HastaExclusivo is null || c.FechaEmision < filtro.HastaExclusivo)
            .Where(c => filtro.Tipo is null || c.TipoComprobante == filtro.Tipo)
            .Where(c => filtro.Estado is null || c.Estado == filtro.Estado)
            .Where(c => filtro.PuntoVenta is null || c.PuntoVenta == filtro.PuntoVenta)
            .Where(c => filtro.ReceptorDocumentoNumero is null
                || c.ReceptorDocumentoNumero == filtro.ReceptorDocumentoNumero)
            .OrderByDescending(c => c.FechaEmision)
            .ThenByDescending(c => c.Numero)
            .ThenBy(c => c.Id)
            .ToList();

        var items = coincidencias
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Task.FromResult<(IReadOnlyList<Comprobante>, long)>((items, coincidencias.Count));
    }

    public Task<MetricasDashboard> ObtenerMetricasAsync(
        Guid contribuyenteId,
        DateTime desde,
        DateTime hastaExclusivo,
        CancellationToken cancellationToken = default)
    {
        ConsultasMetricas++;
        UltimoContribuyenteMetricas = contribuyenteId;
        UltimoDesdeMetricas = desde;
        UltimoHastaMetricas = hastaExclusivo;

        if (ExcepcionMetricas is not null)
        {
            throw ExcepcionMetricas;
        }

        var emitidos = Comprobantes
            .Where(c => c.ContribuyenteId == contribuyenteId
                && c.Estado == EstadoComprobante.Emitido
                && c.FechaEmision >= desde
                && c.FechaEmision < hastaExclusivo)
            .ToList();

        return Task.FromResult(new MetricasDashboard(
            emitidos.Count,
            emitidos.Sum(c => c.ImporteTotal)));
    }

    public Task<Comprobante?> GetByIdempotencyKeyAsync(
        Guid contribuyenteId,
        string clave,
        CancellationToken cancellationToken = default)
        => Task.FromResult(Comprobantes.FirstOrDefault(
            c => c.ContribuyenteId == contribuyenteId && c.IdempotencyKey == clave));

    public Task AddAsync(Comprobante comprobante, CancellationToken cancellationToken = default)
    {
        Comprobantes.Add(comprobante);
        return Task.CompletedTask;
    }

    public Task<bool> AjustarSaldoAsync(
        Guid contribuyenteId,
        Guid originalId,
        decimal? saldoEsperado,
        decimal saldoNuevo,
        CancellationToken cancellationToken = default)
        => Task.FromResult(false);
}

public sealed class ConsultaSecuenciasRepositorioFake : ISecuenciaRepository
{
    public long UltimoNumero { get; set; }

    public long MaximoUltimoNumero { get; set; }

    public int Consultas { get; private set; }

    public Task<long> ObtenerSiguienteNumeroAsync(
        Guid contribuyenteId,
        int puntoVenta,
        TipoComprobante tipo,
        CancellationToken ct = default)
        => throw new NotSupportedException("La consulta no asigna números.");

    public Task<long> ObtenerUltimoNumeroAsync(
        Guid contribuyenteId,
        int puntoVenta,
        TipoComprobante tipo,
        CancellationToken ct = default)
    {
        Consultas++;
        return Task.FromResult(UltimoNumero);
    }

    public Task<long> ObtenerMaximoUltimoNumeroAsync(
        Guid contribuyenteId,
        TipoComprobante tipo,
        CancellationToken ct = default)
    {
        Consultas++;
        return Task.FromResult(MaximoUltimoNumero);
    }

    public Task<long> ObtenerMaximoUltimoNumeroGlobalAsync(
        Guid contribuyenteId,
        CancellationToken ct = default)
    {
        Consultas++;
        return Task.FromResult(MaximoUltimoNumero);
    }
}
