using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using FacturAR.Api.Auth;
using FacturAR.Application.Abstractions;
using FacturAR.Application.ApiKeys;
using FacturAR.Contracts.Comunes;
using FacturAR.Domain.Entities;
using FacturAR.Domain.Enums;
using FacturAR.Infrastructure.Password;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace FacturAR.IntegrationTests;

public sealed class ComprobantePdfEndpointTests
{
    internal const string ApiKeyValida = "fak_0123456789ab_PPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPPP";
    private const string IdInexistente = "9f3f5f70-1c3e-4f0a-9f3e-9d2f0b7a1c22";
    private const string MensajeInterno = "Fallo al componer el PDF: credencial=secreto-123";

    [Fact]
    public async Task Pdf_DeUnComprobantePropio_Devuelve200ConHeadersYFirmaPdf()
    {
        using var factory = new ComprobantePdfFactory();
        var comprobante = NuevoComprobante();
        factory.Comprobantes.Comprobantes.Add(comprobante);
        using var client = factory.CreateClient();

        var response = await EnviarConApiKeyAsync(client, $"/v1/comprobantes/{comprobante.Id}/pdf");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(
            "inline; filename=\"0001-00000001.pdf\"",
            response.Content.Headers.GetValues("Content-Disposition").Single());

        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(bytes, 0, 5));
        Assert.Equal(1, factory.Pdf.Invocaciones);
    }

    [Fact]
    public async Task Pdf_ConPuntoDeVentaDoceYNumeroTrescientosCuarentaYCinco_UsaNombreDeArchivoSeguro()
    {
        using var factory = new ComprobantePdfFactory();
        var comprobante = NuevoComprobante(puntoVenta: 12, numero: 345);
        factory.Comprobantes.Comprobantes.Add(comprobante);
        using var client = factory.CreateClient();

        var response = await EnviarConApiKeyAsync(client, $"/v1/comprobantes/{comprobante.Id}/pdf");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var nombre = response.Content.Headers.GetValues("Content-Disposition").Single();
        Assert.Equal("inline; filename=\"0012-00000345.pdf\"", nombre);
        Assert.Matches("^inline; filename=\"[0-9]{4}-[0-9]{8}\\.pdf\"$", nombre);
    }

    [Fact]
    public async Task Pdf_DeUnComprobanteInexistente_Devuelve404ComprobanteNoEncontrado()
    {
        using var factory = new ComprobantePdfFactory();
        using var client = factory.CreateClient();

        var response = await EnviarConApiKeyAsync(client, $"/v1/comprobantes/{IdInexistente}/pdf");

        await AssertErrorAsync(response, HttpStatusCode.NotFound, "COMPROBANTE_NO_ENCONTRADO");
        Assert.Equal(0, factory.Pdf.Invocaciones);
        Assert.Equal(ComprobantePdfFactory.ContribuyenteId, factory.Comprobantes.UltimoContribuyenteId);
    }

    [Fact]
    public async Task Pdf_DeUnComprobanteDeOtraEmpresa_Devuelve404SinGenerarElDocumento()
    {
        using var factory = new ComprobantePdfFactory();
        var ajeno = NuevoComprobante(contribuyenteId: ComprobantePdfFactory.OtroContribuyenteId);
        factory.Comprobantes.Comprobantes.Add(ajeno);
        using var client = factory.CreateClient();

        var response = await EnviarConApiKeyAsync(client, $"/v1/comprobantes/{ajeno.Id}/pdf");

        await AssertErrorAsync(response, HttpStatusCode.NotFound, "COMPROBANTE_NO_ENCONTRADO");
        Assert.Equal(ComprobantePdfFactory.ContribuyenteId, factory.Comprobantes.UltimoContribuyenteId);
        Assert.Equal(0, factory.Pdf.Invocaciones);

        var cuerpo = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(ajeno.Cae, cuerpo, StringComparison.Ordinal);
        Assert.DoesNotContain(ajeno.ReceptorRazonSocial, cuerpo, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/v1/comprobantes/no-es-guid/pdf")]
    [InlineData("/v1/comprobantes/123/pdf")]
    public async Task Pdf_ConIdQueNoEsGuid_Devuelve400ValidacionSinConsultarElRepositorio(string ruta)
    {
        using var factory = new ComprobantePdfFactory();
        using var client = factory.CreateClient();

        var response = await EnviarConApiKeyAsync(client, ruta);

        await AssertErrorAsync(response, HttpStatusCode.BadRequest, "VALIDACION");
        Assert.Equal(0, factory.Comprobantes.ConsultasDetalle);
        Assert.Equal(0, factory.Pdf.Invocaciones);
    }

    [Fact]
    public async Task Pdf_SinApiKeyNiCookie_Devuelve401ApiKeyAusenteSinGenerarElDocumento()
    {
        using var factory = new ComprobantePdfFactory();
        var comprobante = NuevoComprobante();
        factory.Comprobantes.Comprobantes.Add(comprobante);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/v1/comprobantes/{comprobante.Id}/pdf");

        await AssertErrorAsync(response, HttpStatusCode.Unauthorized, "API_KEY_AUSENTE");
        Assert.Equal(0, factory.Pdf.Invocaciones);
        Assert.Equal(0, factory.Comprobantes.ConsultasDetalle);
    }

    [Fact]
    public async Task Pdf_ConCookieDeRolNoAutorizado_Devuelve403SinPermisos()
    {
        using var factory = new ComprobantePdfFactory();
        var comprobante = NuevoComprobante();
        factory.Comprobantes.Comprobantes.Add(comprobante);
        var cookie = await CrearCookieAsync(factory, rol: "SuperAdmin");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false,
            AllowAutoRedirect = false
        });
        using var mensaje = new HttpRequestMessage(HttpMethod.Get, $"/v1/comprobantes/{comprobante.Id}/pdf");
        mensaje.Headers.TryAddWithoutValidation("Cookie", cookie);

        var response = await client.SendAsync(mensaje);

        await AssertErrorAsync(response, HttpStatusCode.Forbidden, "SIN_PERMISOS");
        Assert.Equal(0, factory.Pdf.Invocaciones);
    }

    [Fact]
    public async Task Pdf_CuandoElServicioDePdfFalla_Devuelve500ErrorInternoSinCuerpoPdfNiDetalle()
    {
        using var factory = new ComprobantePdfFactory();
        var comprobante = NuevoComprobante();
        factory.Comprobantes.Comprobantes.Add(comprobante);
        factory.Pdf.Error = new InvalidOperationException(MensajeInterno);
        using var client = factory.CreateClient();

        var response = await EnviarConApiKeyAsync(client, $"/v1/comprobantes/{comprobante.Id}/pdf");

        await AssertErrorAsync(response, HttpStatusCode.InternalServerError, "ERROR_INTERNO");

        var cuerpo = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(MensajeInterno, cuerpo, StringComparison.Ordinal);
        Assert.DoesNotContain("secreto-123", cuerpo, StringComparison.Ordinal);
        Assert.DoesNotContain("%PDF", cuerpo, StringComparison.Ordinal);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(1, factory.Pdf.Invocaciones);

        var entrada = Assert.Single(
            factory.Logs.Entradas,
            e => e.Nivel == LogLevel.Error && e.Categoria == "FacturAR.Api.Comprobantes");
        Assert.Contains(MensajeInterno, entrada.Excepcion, StringComparison.Ordinal);
    }

    private static async Task<string> CrearCookieAsync(ComprobantePdfFactory factory, string rol)
    {
        using var scope = factory.Services.CreateScope();
        var contexto = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        var autenticacion = scope.ServiceProvider.GetRequiredService<IAuthenticationService>();

        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim("contribuyenteId", ComprobantePdfFactory.ContribuyenteId.ToString()),
            new Claim("rol", rol),
            new Claim("plano", "Empresa")
        ], authenticationType: CookieAuthenticationExtensions.EmpresaScheme));

        await autenticacion.SignInAsync(
            contexto,
            CookieAuthenticationExtensions.EmpresaScheme,
            principal,
            new AuthenticationProperties());

        var setCookie = contexto.Response.Headers.SetCookie.ToString();
        Assert.False(string.IsNullOrWhiteSpace(setCookie));

        return setCookie.Split(';')[0];
    }

    private static Comprobante NuevoComprobante(
        Guid? contribuyenteId = null,
        int puntoVenta = 1,
        long numero = 1)
        => new()
        {
            ContribuyenteId = contribuyenteId ?? ComprobantePdfFactory.ContribuyenteId,
            EmisorCuit = ComprobantePdfFactory.CuitEmisor,
            EmisorRazonSocial = "Demo SRL",
            PuntoVenta = puntoVenta,
            TipoComprobante = TipoComprobante.FacturaA,
            Numero = numero,
            FechaEmision = new DateTime(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc),
            Cae = "12345678901234",
            CaeVencimiento = new DateTime(2026, 11, 4, 0, 0, 0, DateTimeKind.Utc),
            ReceptorDocumentoTipo = "CUIT",
            ReceptorDocumentoNumero = "20345678901",
            ReceptorRazonSocial = "Cliente S.A.",
            ReceptorCondicionIva = CondicionIva.ResponsableInscripto,
            ImporteNeto = 3000m,
            ImporteIva = 630m,
            ImporteTotal = 3630m,
            Moneda = "ARS",
            Estado = EstadoComprobante.Emitido,
            IdempotencyKey = "idem-interna-no-expuesta",
            Items =
            [
                new ComprobanteItem
                {
                    Descripcion = "Servicio",
                    Cantidad = 1m,
                    PrecioUnitario = 3000m,
                    AlicuotaIva = 21m,
                    Subtotal = 3000m
                }
            ]
        };

    private static async Task<HttpResponseMessage> EnviarConApiKeyAsync(HttpClient client, string ruta)
    {
        using var mensaje = new HttpRequestMessage(HttpMethod.Get, ruta);
        mensaje.Headers.TryAddWithoutValidation("X-Api-Key", ApiKeyValida);

        return await client.SendAsync(mensaje);
    }

    private static async Task<ErrorResponse> AssertErrorAsync(
        HttpResponseMessage response,
        HttpStatusCode status,
        string codigo)
    {
        Assert.Equal(status, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal(codigo, error.Codigo);
        return error;
    }
}

public sealed class ComprobantePdfFactory : WebApplicationFactory<Program>
{
    public const string CuitEmisor = "20123456786";

    public static readonly Guid ContribuyenteId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    public static readonly Guid OtroContribuyenteId = Guid.Parse("88888888-8888-8888-8888-888888888888");
    public static readonly Guid ApiKeyId = Guid.Parse("66666666-6666-6666-6666-666666666666");

    private readonly PasswordHasher _hasher = new(1000);

    public ComprobantePdfFactory()
    {
        Contribuyentes.Contribuyentes.Add(new Contribuyente
        {
            Id = ContribuyenteId,
            RazonSocial = "Demo SRL",
            Cuit = CuitEmisor,
            CondicionIva = CondicionIva.ResponsableInscripto,
            Activo = true
        });
        Contribuyentes.Contribuyentes.Add(new Contribuyente
        {
            Id = OtroContribuyenteId,
            RazonSocial = "Otra SRL",
            Cuit = "20111111112",
            CondicionIva = CondicionIva.ResponsableInscripto,
            Activo = true
        });
        Usuarios.Usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            ContribuyenteId = ContribuyenteId,
            Email = "consulta@demo.com",
            PasswordHash = _hasher.Hash("Secreta123!"),
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

    public PdfComprobanteFake Pdf { get; } = new();

    public PdfLogCollector Logs { get; } = new();

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
                    [ComprobantePdfEndpointTests.ApiKeyValida] =
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

            services.RemoveAll<IPdfComprobanteService>();
            services.AddSingleton<IPdfComprobanteService>(Pdf);

            services.AddSingleton<ILoggerProvider>(Logs);
        });
    }
}

public sealed class PdfComprobanteFake : IPdfComprobanteService
{
    public byte[] Bytes { get; set; } = Encoding.ASCII.GetBytes("%PDF-1.4\n% documento simulado de prueba\n%%EOF\n");

    public Exception? Error { get; set; }

    public List<Comprobante> Generados { get; } = [];

    public int Invocaciones => Generados.Count;

    public Task<byte[]> GenerarAsync(Comprobante comprobante, CancellationToken ct = default)
    {
        Generados.Add(comprobante);

        return Error is null
            ? Task.FromResult(Bytes)
            : Task.FromException<byte[]>(Error);
    }
}

public sealed record EntradaPdfLog(string Categoria, LogLevel Nivel, string Mensaje, string Excepcion);

public sealed class PdfLogCollector : ILoggerProvider
{
    private readonly ConcurrentQueue<EntradaPdfLog> _entradas = new();

    public IReadOnlyCollection<EntradaPdfLog> Entradas => _entradas;

    public ILogger CreateLogger(string categoryName) => new Colector(categoryName, _entradas);

    public void Dispose()
    {
    }

    private sealed class Colector : ILogger
    {
        private readonly string _categoria;
        private readonly ConcurrentQueue<EntradaPdfLog> _entradas;

        public Colector(string categoria, ConcurrentQueue<EntradaPdfLog> entradas)
        {
            _categoria = categoria;
            _entradas = entradas;
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
            => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => _entradas.Enqueue(new EntradaPdfLog(
                _categoria,
                logLevel,
                formatter(state, exception),
                exception?.ToString() ?? string.Empty));
    }
}
