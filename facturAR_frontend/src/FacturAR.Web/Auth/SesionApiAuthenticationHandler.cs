using System.Security.Claims;
using System.Text.Encodings.Web;
using FacturAR.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace FacturAR.Web.Auth;

public sealed class SesionApiAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "SesionApi";

    private const string NombreAdminPorDefecto = ".FacturAR.Admin";
    private const string NombreContribuyentePorDefecto = ".FacturAR.Contribuyente";

    private readonly FacturarApiClient _api;
    private readonly string _cookieAdmin;
    private readonly string _cookieContribuyente;

    public SesionApiAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        FacturarApiClient api,
        IConfiguration configuration)
        : base(options, logger, encoder)
    {
        _api = api;
        _cookieAdmin = configuration["Auth:Cookie:NombreAdmin"] ?? NombreAdminPorDefecto;
        _cookieContribuyente = configuration["Auth:Cookie:NombreContribuyente"] ?? NombreContribuyentePorDefecto;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Context.GetEndpoint()?.Metadata.GetMetadata<IAuthorizeData>() is null)
        {
            return AuthenticateResult.NoResult();
        }

        var cookie = Request.Headers.Cookie.ToString();
        if (string.IsNullOrWhiteSpace(cookie))
        {
            return AuthenticateResult.NoResult();
        }

        var haySesionAdmin = Request.Cookies.ContainsKey(_cookieAdmin);
        var haySesionEmpresa = Request.Cookies.ContainsKey(_cookieContribuyente);
        if (!haySesionAdmin && !haySesionEmpresa)
        {
            return AuthenticateResult.NoResult();
        }

        var claims = new List<Claim>();
        try
        {
            if (haySesionEmpresa)
            {
                var empresa = await _api.GetMeEmpresaAsync(cookie, Context.RequestAborted);
                if (empresa is not null)
                {
                    claims.Add(new Claim(ClaimTypes.NameIdentifier, empresa.UsuarioId.ToString()));
                    claims.Add(new Claim(ClaimTypes.Name, empresa.Email));
                    claims.Add(new Claim("email", empresa.Email));
                    claims.Add(new Claim("rol", empresa.Rol));
                    claims.Add(new Claim("contribuyenteId", empresa.ContribuyenteId.ToString()));
                    claims.Add(new Claim("plano", "Empresa"));
                }
            }

            if (haySesionAdmin)
            {
                var admin = await _api.GetMeAdminAsync(cookie, Context.RequestAborted);
                if (admin is not null)
                {
                    claims.Add(new Claim(ClaimTypes.NameIdentifier, admin.AdministradorId.ToString()));
                    claims.Add(new Claim(ClaimTypes.Name, admin.Email));
                    claims.Add(new Claim("email", admin.Email));
                    claims.Add(new Claim("rolGlobal", admin.RolGlobal));
                    claims.Add(new Claim("plano", "Administrador"));
                }
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            Logger.LogWarning(ex, "No se pudo validar la sesión del portal contra la API.");
            return AuthenticateResult.NoResult();
        }

        if (claims.Count == 0)
        {
            return AuthenticateResult.NoResult();
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
        return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
        => RedirigirAsync();

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
        => RedirigirAsync();

    private Task RedirigirAsync()
    {
        var esAdmin = Request.Path.StartsWithSegments("/admin", StringComparison.OrdinalIgnoreCase);
        var destino = esAdmin ? "/admin/login" : "/login";

        if (esAdmin && !Request.Path.StartsWithSegments("/admin/login", StringComparison.OrdinalIgnoreCase))
        {
            var returnUrl = $"{Request.PathBase}{Request.Path}{Request.QueryString}";
            if (!string.IsNullOrWhiteSpace(returnUrl))
            {
                destino = $"{destino}?returnUrl={Uri.EscapeDataString(returnUrl)}";
            }
        }

        Response.Redirect(destino);
        return Task.CompletedTask;
    }
}
