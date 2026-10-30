using System.Security.Claims;
using System.Text.Encodings.Web;
using FacturAR.Application.Abstractions;
using FacturAR.Application.ApiKeys;
using FacturAR.Contracts.Comunes;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace FacturAR.Api.Auth;

public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "ApiKey";
    public const string ClaimContribuyenteId = "contribuyenteId";
    public const string ClaimApiKeyId = "apikeyId";
    public const string ClaimPlano = "plano";
    public const string Plano = "ApiKey";

    private const string MensajeMongoNoDisponible = "No hay conexión con la base de datos. Intentá de nuevo en unos minutos.";

    private readonly IApiKeyAuthenticator _authenticator;
    private readonly ApiKeysOptions _apiKeysOptions;
    private readonly ApiKeyUsageRegistrar _usageRegistrar;

    private bool _mongoNoDisponible;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IApiKeyAuthenticator authenticator,
        ApiKeysOptions apiKeysOptions,
        ApiKeyUsageRegistrar usageRegistrar)
        : base(options, logger, encoder)
    {
        _authenticator = authenticator;
        _apiKeysOptions = apiKeysOptions;
        _usageRegistrar = usageRegistrar;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var valor = Request.Headers[_apiKeysOptions.HeaderName].ToString();
        if (string.IsNullOrWhiteSpace(valor))
        {
            return AuthenticateResult.NoResult();
        }

        ResultadoApiKey? resultado;
        try
        {
            resultado = await _authenticator.AutenticarAsync(valor, Context.RequestAborted);
        }
        catch (Exception ex) when (ex is MongoConnectionException or TimeoutException)
        {
            _mongoNoDisponible = true;
            Logger.LogError(ex, "No se pudo consultar MongoDB durante la autenticación con API key.");
            return AuthenticateResult.Fail("MongoDB no disponible.");
        }

        if (resultado is null)
        {
            Logger.LogDebug("Intento de autenticación con API key rechazado.");
            return AuthenticateResult.Fail("API key inválida.");
        }

        var identidad = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, resultado.ApiKeyId.ToString()),
            new Claim(ClaimContribuyenteId, resultado.ContribuyenteId.ToString()),
            new Claim(ClaimApiKeyId, resultado.ApiKeyId.ToString()),
            new Claim(ClaimPlano, Plano)
        ], SchemeName);

        _usageRegistrar.Registrar(resultado.ApiKeyId, resultado.ContribuyenteId);

        return AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identidad), SchemeName));
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        if (Response.HasStarted)
        {
            return;
        }

        var error = _mongoNoDisponible
            ? new ErrorResponse("MONGO_NO_DISPONIBLE", MensajeMongoNoDisponible)
            : CrearErrorCredenciales();

        Response.StatusCode = _mongoNoDisponible
            ? StatusCodes.Status503ServiceUnavailable
            : StatusCodes.Status401Unauthorized;
        await Response.WriteAsJsonAsync(error, Context.RequestAborted);
    }

    private ErrorResponse CrearErrorCredenciales()
    {
        var valor = Request.Headers[_apiKeysOptions.HeaderName].ToString();

        return string.IsNullOrWhiteSpace(valor)
            ? new ErrorResponse(
                "API_KEY_AUSENTE",
                $"Se requiere el header {_apiKeysOptions.HeaderName}.")
            : new ErrorResponse("API_KEY_INVALIDA", "La API key no es válida.");
    }
}
