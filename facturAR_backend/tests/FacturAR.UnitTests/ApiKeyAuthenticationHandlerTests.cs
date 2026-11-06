using System.Security.Claims;
using System.Text.Encodings.Web;
using FacturAR.Api.Auth;
using FacturAR.Application.Abstractions;
using FacturAR.Application.ApiKeys;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FacturAR.UnitTests;

public sealed class ApiKeyAuthenticationHandlerTests
{
    [Fact]
    public async Task SinHeader_NoAutenticaYElChallengeDevuelveApiKeyAusente()
    {
        var contexto = new ApiKeysContexto();
        var http = new DefaultHttpContext();
        var autenticador = new FakeApiKeyAuthenticator(_ => null);
        var handler = await CrearHandlerAsync(
            contexto,
            autenticador,
            new FabricaRegistroCaptor(),
            http);

        var resultado = await handler.AuthenticateAsync();

        Assert.False(resultado.Succeeded);
        Assert.True(resultado.None);
        Assert.Equal(0, autenticador.Llamadas);

        await handler.ChallengeAsync(new AuthenticationProperties());
        var respuesta = await LeerRespuestaAsync(http);

        Assert.Equal(StatusCodes.Status401Unauthorized, http.Response.StatusCode);
        Assert.Contains("API_KEY_AUSENTE", respuesta, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HeaderInvalido_ElChallengeDevuelveApiKeyInvalida()
    {
        var contexto = new ApiKeysContexto();
        var http = new DefaultHttpContext();
        http.Request.Headers[contexto.Options.HeaderName] = "no-es-una-key";
        var handler = await CrearHandlerAsync(
            contexto,
            new FakeApiKeyAuthenticator(_ => null),
            new FabricaRegistroCaptor(),
            http);

        var resultado = await handler.AuthenticateAsync();

        Assert.False(resultado.Succeeded);
        Assert.NotNull(resultado.Failure);

        await handler.ChallengeAsync(new AuthenticationProperties());
        var respuesta = await LeerRespuestaAsync(http);

        Assert.Equal(StatusCodes.Status401Unauthorized, http.Response.StatusCode);
        Assert.Contains("API_KEY_INVALIDA", respuesta, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HeaderMalFormado_NoConsultaElRepositorioYDevuelveApiKeyInvalida()
    {
        var contexto = new ApiKeysContexto();
        var http = new DefaultHttpContext();
        var valor = "no-es-una-key";
        http.Request.Headers[contexto.Options.HeaderName] = valor;
        var logs = new FabricaRegistroCaptor();
        var autenticador = new ApiKeyAuthenticator(
            contexto.ApiKeys,
            contexto.Contribuyentes,
            contexto.Hasher,
            contexto.Options,
            NullLogger<ApiKeyAuthenticator>.Instance);
        var handler = await CrearHandlerAsync(contexto, autenticador, logs, http);

        var resultado = await handler.AuthenticateAsync();

        Assert.False(resultado.Succeeded);
        Assert.Equal(0, contexto.ApiKeys.ConsultasPorPrefijo);

        await handler.ChallengeAsync(new AuthenticationProperties());
        var respuesta = await LeerRespuestaAsync(http);

        Assert.Equal(StatusCodes.Status401Unauthorized, http.Response.StatusCode);
        Assert.Contains("API_KEY_INVALIDA", respuesta, StringComparison.Ordinal);
        Assert.DoesNotContain(logs.Mensajes, m => m.Contains(valor, StringComparison.Ordinal));
    }

    [Fact]
    public async Task KeyValida_ConFalloDeActualizacionDelUso_SigueAutenticando()
    {
        var contexto = new ApiKeysContexto();
        var key = contexto.AgregarKey();
        var http = new DefaultHttpContext();
        http.Request.Headers[contexto.Options.HeaderName] = $"fak_{key.Prefix}_{new string('A', 43)}";
        var repositorio = new SpyApiKeyRepository
        {
            AlActualizar = (_, _, _, _, _) => throw new TimeoutException("Mongo no responde")
        };
        var options = new ApiKeysOptions
        {
            HeaderName = contexto.Options.HeaderName,
            ActualizarUltimoUso = true
        };
        var logger = new RegistroConNiveles<ApiKeyUsageRegistrar>();
        var registrar = CrearRegistrador(repositorio, options, logger);
        var handler = await CrearHandlerAsync(
            contexto,
            new FakeApiKeyAuthenticator(_ => new ResultadoApiKey(key.Id, key.ContribuyenteId)),
            new FabricaRegistroCaptor(),
            http,
            options,
            registrar);

        var resultado = await handler.AuthenticateAsync();

        Assert.True(resultado.Succeeded);
        await EsperarAsync(() => logger.Entradas.Any(e => e.Nivel == LogLevel.Warning));
        Assert.Equal(1, repositorio.Llamadas);
    }

    [Fact]
    public async Task FalloDeInfraestructura_ElChallengeDevuelveMongoNoDisponible()
    {
        var contexto = new ApiKeysContexto();
        var http = new DefaultHttpContext();
        http.Request.Headers[contexto.Options.HeaderName] = $"fak_0123456789ab_{new string('A', 43)}";
        var handler = await CrearHandlerAsync(
            contexto,
            new FakeApiKeyAuthenticator(_ => throw new TimeoutException("sin conexion")),
            new FabricaRegistroCaptor(),
            http);

        var resultado = await handler.AuthenticateAsync();

        Assert.False(resultado.Succeeded);
        Assert.NotNull(resultado.Failure);

        await handler.ChallengeAsync(new AuthenticationProperties());
        var respuesta = await LeerRespuestaAsync(http);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, http.Response.StatusCode);
        Assert.Contains("MONGO_NO_DISPONIBLE", respuesta, StringComparison.Ordinal);
    }

    [Fact]
    public async Task KeyValida_EmiteLosClaimsEsperados()
    {
        var contexto = new ApiKeysContexto();
        var key = contexto.AgregarKey();
        var http = new DefaultHttpContext();
        http.Request.Headers[contexto.Options.HeaderName] = $"fak_{key.Prefix}_{new string('A', 43)}";
        var handler = await CrearHandlerAsync(
            contexto,
            new FakeApiKeyAuthenticator(_ => new ResultadoApiKey(key.Id, key.ContribuyenteId)),
            new FabricaRegistroCaptor(),
            http);

        var resultado = await handler.AuthenticateAsync();

        Assert.True(resultado.Succeeded);
        var principal = resultado.Principal;
        Assert.NotNull(principal);
        Assert.Equal(key.Id.ToString(), principal.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        Assert.Equal(key.ContribuyenteId.ToString(), principal.FindFirst("contribuyenteId")?.Value);
        Assert.Equal(key.Id.ToString(), principal.FindFirst("apikeyId")?.Value);
        Assert.Equal("ApiKey", principal.FindFirst("plano")?.Value);
    }

    [Fact]
    public async Task KeyValida_NoRegistraElValorEnLosLogs()
    {
        var contexto = new ApiKeysContexto();
        var key = contexto.AgregarKey();
        var valor = $"fak_{key.Prefix}_{new string('A', 43)}";
        var http = new DefaultHttpContext();
        http.Request.Headers[contexto.Options.HeaderName] = valor;
        var logs = new FabricaRegistroCaptor();
        var handler = await CrearHandlerAsync(
            contexto,
            new FakeApiKeyAuthenticator(_ => new ResultadoApiKey(key.Id, key.ContribuyenteId)),
            logs,
            http);

        await handler.AuthenticateAsync();

        Assert.DoesNotContain(logs.Mensajes, m => m.Contains(valor, StringComparison.Ordinal));
        Assert.DoesNotContain(logs.Mensajes, m => m.Contains(key.Hash, StringComparison.Ordinal));
    }

    private static async Task<ApiKeyAuthenticationHandler> CrearHandlerAsync(
        ApiKeysContexto contexto,
        IApiKeyAuthenticator autenticador,
        FabricaRegistroCaptor logs,
        DefaultHttpContext http,
        ApiKeysOptions? options = null,
        ApiKeyUsageRegistrar? registrador = null)
    {
        http.RequestServices = new ServiceCollection().AddOptions().BuildServiceProvider();
        http.Response.Body = new MemoryStream();

        options ??= new ApiKeysOptions
        {
            HeaderName = contexto.Options.HeaderName,
            ActualizarUltimoUso = false
        };

        var handler = new ApiKeyAuthenticationHandler(
            new MonitorOpciones<AuthenticationSchemeOptions>(new AuthenticationSchemeOptions()),
            logs,
            UrlEncoder.Default,
            autenticador,
            options,
            registrador ?? CrearRegistrador(options));

        await handler.InitializeAsync(
            new AuthenticationScheme(
                ApiKeyAuthenticationHandler.SchemeName,
                null,
                typeof(ApiKeyAuthenticationHandler)),
            http);

        return handler;
    }

    private static ApiKeyUsageRegistrar CrearRegistrador(ApiKeysOptions options)
        => CrearRegistrador(new FakeApiKeyRepository(), options, NullLogger<ApiKeyUsageRegistrar>.Instance);

    private static ApiKeyUsageRegistrar CrearRegistrador(
        IApiKeyRepository repositorio,
        ApiKeysOptions options,
        ILogger<ApiKeyUsageRegistrar> logger)
    {
        var services = new ServiceCollection();
        services.AddSingleton(repositorio);
        var provider = services.BuildServiceProvider();

        return new ApiKeyUsageRegistrar(
            provider.GetRequiredService<IServiceScopeFactory>(),
            logger,
            options);
    }

    private static async Task EsperarAsync(Func<bool> condicion, int timeoutMs = 5000)
    {
        var inicio = Environment.TickCount64;

        while (!condicion())
        {
            if (Environment.TickCount64 - inicio > timeoutMs)
            {
                throw new TimeoutException("No se cumplió la condición dentro del tiempo esperado.");
            }

            await Task.Delay(20);
        }
    }

    private static async Task<string> LeerRespuestaAsync(HttpContext http)
    {
        http.Response.Body.Position = 0;
        using var reader = new StreamReader(http.Response.Body);
        return await reader.ReadToEndAsync();
    }
}

internal sealed class FakeApiKeyAuthenticator : IApiKeyAuthenticator
{
    private readonly Func<string, ResultadoApiKey?> _resultado;

    public FakeApiKeyAuthenticator(Func<string, ResultadoApiKey?> resultado)
    {
        _resultado = resultado;
    }

    public int Llamadas { get; private set; }

    public string? UltimoValor { get; private set; }

    public Task<ResultadoApiKey?> AutenticarAsync(
        string valorPresentado,
        CancellationToken ct = default)
    {
        Llamadas++;
        UltimoValor = valorPresentado;
        return Task.FromResult(_resultado(valorPresentado));
    }
}

internal sealed class MonitorOpciones<T> : IOptionsMonitor<T>
{
    public MonitorOpciones(T valor)
    {
        CurrentValue = valor;
    }

    public T CurrentValue { get; }

    public T Get(string? name) => CurrentValue;

    public IDisposable OnChange(Action<T, string?> listener) => new SinSuscripcion();

    private sealed class SinSuscripcion : IDisposable
    {
        public void Dispose()
        {
        }
    }
}

internal sealed class FabricaRegistroCaptor : ILoggerFactory
{
    public IReadOnlyList<string> Mensajes => _mensajes;

    private readonly List<string> _mensajes = [];

    public void AddProvider(ILoggerProvider provider)
    {
    }

    public ILogger CreateLogger(string categoryName) => new RegistroCaptor<object>(_mensajes);

    public void Dispose()
    {
    }
}

