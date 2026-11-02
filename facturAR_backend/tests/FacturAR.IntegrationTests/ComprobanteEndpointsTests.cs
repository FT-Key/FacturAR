using System.Net;
using System.Net.Http.Json;
using System.Text;
using FacturAR.Application.Abstractions;
using FacturAR.Application.ApiKeys;
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

public sealed class ComprobanteEndpointsTests
{
    [Fact]
    public async Task Emitir_SinHeaderDeApiKey_Devuelve401ApiKeyAusente()
    {
        using var factory = new ComprobanteApiKeyFactory();
        using var client = factory.CreateClient();

        var response = await EnviarAsync(client, FacturaA(), "idem-sin-key", apiKey: null);

        await AssertErrorAsync(response, HttpStatusCode.Unauthorized, "API_KEY_AUSENTE");
    }

    [Fact]
    public async Task Emitir_ConApiKeyInexistente_Devuelve401ApiKeyInvalida()
    {
        using var factory = new ComprobanteApiKeyFactory();
        using var client = factory.CreateClient();

        var response = await EnviarAsync(
            client,
            FacturaA(),
            "idem-key-invalida",
            ComprobanteApiKeyFactory.ApiKeyDesconocida);

        await AssertErrorAsync(response, HttpStatusCode.Unauthorized, "API_KEY_INVALIDA");
    }

    [Fact]
    public async Task Emitir_SinIdempotencyKey_Devuelve400Validacion()
    {
        using var factory = new ComprobanteApiKeyFactory();
        using var client = factory.CreateClient();

        var response = await EnviarAsync(
            client,
            FacturaA(),
            idempotencyKey: null,
            ComprobanteApiKeyFactory.ApiKeyValida);

        var error = await AssertErrorAsync(response, HttpStatusCode.BadRequest, "VALIDACION");
        Assert.Equal("Falta el header Idempotency-Key.", error.Mensaje);
        Assert.Empty(factory.Eventos.Eventos);
    }

    [Fact]
    public async Task Emitir_ConIdempotencyKeyDe129Caracteres_Devuelve400Validacion()
    {
        using var factory = new ComprobanteApiKeyFactory();
        using var client = factory.CreateClient();

        var response = await EnviarAsync(
            client,
            FacturaA(),
            new string('a', 129),
            ComprobanteApiKeyFactory.ApiKeyValida);

        var error = await AssertErrorAsync(response, HttpStatusCode.BadRequest, "VALIDACION");
        Assert.Equal("El header Idempotency-Key admite hasta 128 caracteres.", error.Mensaje);
        Assert.Empty(factory.Eventos.Eventos);
    }

    [Fact]
    public async Task Emitir_ConCookieDePortalSinApiKey_Devuelve401ApiKeyAusente()
    {
        using var factory = new ComprobanteApiKeyFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false,
            AllowAutoRedirect = false
        });

        var login = await client.PostAsJsonAsync(
            "/v1/auth/login",
            new LoginRequest(ComprobanteApiKeyFactory.EmailUsuario, ComprobanteApiKeyFactory.PasswordUsuario));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var cookie = login.Headers.GetValues("Set-Cookie").First().Split(';')[0];

        using var mensaje = new HttpRequestMessage(HttpMethod.Post, "/v1/comprobantes")
        {
            Content = JsonContent.Create(FacturaA())
        };
        mensaje.Headers.TryAddWithoutValidation("Idempotency-Key", "idem-cookie");
        mensaje.Headers.TryAddWithoutValidation("Cookie", cookie);

        var response = await client.SendAsync(mensaje);

        await AssertErrorAsync(response, HttpStatusCode.Unauthorized, "API_KEY_AUSENTE");
    }

    [Fact]
    public async Task Emitir_ConItemsConElementoNulo_Devuelve400Validacion()
    {
        using var factory = new ComprobanteApiKeyFactory();
        using var client = factory.CreateClient();

        const string cuerpo = """
        {
          "tipoComprobante": 1,
          "puntoVenta": 1,
          "receptor": {
            "documentoTipo": "CUIT",
            "documentoNumero": "30712345671",
            "razonSocial": "Cliente S.A.",
            "condicionIva": 1
          },
          "items": [null]
        }
        """;

        using var mensaje = new HttpRequestMessage(HttpMethod.Post, "/v1/comprobantes")
        {
            Content = new StringContent(cuerpo, Encoding.UTF8, "application/json")
        };
        mensaje.Headers.TryAddWithoutValidation("X-Api-Key", ComprobanteApiKeyFactory.ApiKeyValida);
        mensaje.Headers.TryAddWithoutValidation("Idempotency-Key", "idem-items-nulos");

        var response = await client.SendAsync(mensaje);

        var error = await AssertErrorAsync(response, HttpStatusCode.BadRequest, "VALIDACION");
        Assert.Equal("Los ítems del comprobante no son válidos.", error.Mensaje);
        Assert.Empty(factory.Eventos.Eventos);
    }

    [Theory]
    [InlineData("{ \"tipoComprobante\": ")]
    [InlineData("null")]
    public async Task Emitir_ConBodyMalformado_Devuelve400Validacion(string cuerpo)
    {
        using var factory = new ComprobanteApiKeyFactory();
        using var client = factory.CreateClient();

        using var mensaje = new HttpRequestMessage(HttpMethod.Post, "/v1/comprobantes")
        {
            Content = new StringContent(cuerpo, Encoding.UTF8, "application/json")
        };
        mensaje.Headers.TryAddWithoutValidation("X-Api-Key", ComprobanteApiKeyFactory.ApiKeyValida);
        mensaje.Headers.TryAddWithoutValidation("Idempotency-Key", "idem-body-malformado");

        var response = await client.SendAsync(mensaje);

        var error = await AssertErrorAsync(response, HttpStatusCode.BadRequest, "VALIDACION");
        Assert.Equal("El cuerpo de la solicitud no es válido.", error.Mensaje);
        Assert.Empty(factory.Eventos.Eventos);
    }

    private static EmitirComprobanteRequest FacturaA()
        => new(
            (int)TipoComprobante.FacturaA,
            1,
            null,
            new ReceptorComprobanteRequest(
                "CUIT",
                ComprobanteApiKeyFactory.CuitReceptor,
                "Cliente S.A.",
                (int)CondicionIva.ResponsableInscripto),
            [new ItemComprobanteRequest("Servicio", 2m, 5000m, 21m)],
            12100m,
            null);

    private static async Task<HttpResponseMessage> EnviarAsync(
        HttpClient client,
        EmitirComprobanteRequest request,
        string? idempotencyKey,
        string? apiKey)
    {
        using var mensaje = new HttpRequestMessage(HttpMethod.Post, "/v1/comprobantes")
        {
            Content = JsonContent.Create(request)
        };

        if (idempotencyKey is not null)
        {
            mensaje.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        }

        if (apiKey is not null)
        {
            mensaje.Headers.TryAddWithoutValidation("X-Api-Key", apiKey);
        }

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

public sealed class ComprobanteApiKeyFactory : WebApplicationFactory<Program>
{
    public const string ApiKeyValida = "fak_0123456789ab_AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
    public const string ApiKeyDesconocida = "fak_ffffffffffff_BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB";
    public const string EmailUsuario = "admin@demo.com";
    public const string PasswordUsuario = "Secreta123!";
    public const string CuitEmisor = "20123456786";
    public const string CuitReceptor = "30712345671";

    public static readonly Guid ContribuyenteId = Guid.NewGuid();
    public static readonly Guid UsuarioId = Guid.NewGuid();
    public static readonly Guid ApiKeyId = Guid.NewGuid();

    private readonly PasswordHasher _hasher = new(1000);

    public ComprobanteApiKeyFactory()
    {
        Contribuyentes.Contribuyentes.Add(new Contribuyente
        {
            Id = ContribuyenteId,
            RazonSocial = "Demo SRL",
            Cuit = CuitEmisor,
            CondicionIva = CondicionIva.ResponsableInscripto,
            Activo = true
        });
        PuntosVenta.PuntosVenta.Add(new PuntoVenta
        {
            ContribuyenteId = ContribuyenteId,
            Numero = 1,
            Descripcion = "Principal",
            Activo = true
        });
        Usuarios.Usuario = new Usuario
        {
            Id = UsuarioId,
            ContribuyenteId = ContribuyenteId,
            Email = EmailUsuario,
            PasswordHash = _hasher.Hash(PasswordUsuario),
            Rol = RolUsuario.Administrador,
            Activo = true
        };
    }

    public ComprobanteContribuyenteFake Contribuyentes { get; } = new();

    public ComprobantePuntoVentaFake PuntosVenta { get; } = new();

    public ComprobanteParametrosFake Parametros { get; } = new();

    public ComprobanteUsuarioFake Usuarios { get; } = new();

    public ComprobanteEventoFake Eventos { get; } = new();

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
                    [ApiKeyValida] = new ResultadoApiKey(ApiKeyId, ContribuyenteId)
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
        });
    }
}

public sealed class ComprobanteApiKeyAuthenticatorFake : IApiKeyAuthenticator
{
    private readonly IReadOnlyDictionary<string, ResultadoApiKey> _validas;

    public ComprobanteApiKeyAuthenticatorFake(IReadOnlyDictionary<string, ResultadoApiKey> validas)
    {
        _validas = validas;
    }

    public Task<ResultadoApiKey?> AutenticarAsync(string valorPresentado, CancellationToken ct = default)
        => Task.FromResult(
            _validas.TryGetValue(valorPresentado, out var resultado) ? resultado : null);
}

public sealed class ComprobanteContribuyenteFake : IContribuyenteRepository
{
    public List<Contribuyente> Contribuyentes { get; } = [];

    public Task<Contribuyente?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(Contribuyentes.FirstOrDefault(c => c.Id == id));

    public Task<bool> ExistePorCuitAsync(string cuit, CancellationToken ct = default)
        => Task.FromResult(Contribuyentes.Any(c => c.Cuit == cuit));

    public Task InsertarAsync(Contribuyente contribuyente, CancellationToken ct = default)
    {
        Contribuyentes.Add(contribuyente);
        return Task.CompletedTask;
    }
}

public sealed class ComprobantePuntoVentaFake : IPuntoVentaRepository
{
    public List<PuntoVenta> PuntosVenta { get; } = [];

    public Task InsertarAsync(PuntoVenta puntoVenta, CancellationToken ct = default)
    {
        PuntosVenta.Add(puntoVenta);
        return Task.CompletedTask;
    }

    public Task<PuntoVenta?> ObtenerPorNumeroAsync(
        Guid contribuyenteId,
        int numero,
        CancellationToken ct = default)
        => Task.FromResult(PuntosVenta.FirstOrDefault(
            p => p.ContribuyenteId == contribuyenteId && p.Numero == numero));
}

public sealed class ComprobanteParametrosFake : IParametrosResolver
{
    public Task<string?> ObtenerAsync(
        string clave,
        Guid? contribuyenteId,
        CancellationToken ct = default)
        => Task.FromResult<string?>(null);

    public Task<int> ObtenerDiasVencimientoCaeAsync(Guid? contribuyenteId, CancellationToken ct = default)
        => Task.FromResult(10);

    public Task<IReadOnlyList<decimal>> ObtenerAlicuotasPermitidasAsync(
        Guid? contribuyenteId,
        CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<decimal>>([0m, 5m, 10.5m, 21m, 27m]);

    public Task<string> ObtenerMonedaAsync(Guid? contribuyenteId, CancellationToken ct = default)
        => Task.FromResult("ARS");
}

public sealed class ComprobanteUsuarioFake : IUsuarioRepository
{
    public Usuario? Usuario { get; set; }

    public Task<Usuario?> GetByEmailAsync(string email, CancellationToken ct = default)
        => Task.FromResult(
            Usuario is not null && string.Equals(Usuario.Email, email, StringComparison.OrdinalIgnoreCase)
                ? Usuario
                : null);

    public Task<Usuario?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(Usuario is not null && Usuario.Id == id ? Usuario : null);

    public Task InsertAsync(Usuario usuario, CancellationToken ct = default)
    {
        Usuario = usuario;
        return Task.CompletedTask;
    }

    public Task<bool> ExistePorContribuyenteYEmailAsync(
        Guid contribuyenteId,
        string email,
        CancellationToken ct = default)
        => Task.FromResult(false);

    public Task UpdateAsync(Usuario usuario, CancellationToken ct = default)
    {
        Usuario = usuario;
        return Task.CompletedTask;
    }
}

public sealed class ComprobanteEventoFake : IEventoAuditoriaRepository
{
    public List<EventoAuditoria> Eventos { get; } = [];

    public Task AddAsync(EventoAuditoria evento, CancellationToken ct = default)
    {
        Eventos.Add(evento);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<EventoAuditoria>> ListarPorEntidadAsync(
        string entidad,
        string entidadId,
        CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<EventoAuditoria>>(
            Eventos.Where(e => e.Entidad == entidad && e.EntidadId == entidadId).ToList());
}
