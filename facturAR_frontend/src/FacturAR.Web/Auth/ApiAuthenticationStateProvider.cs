using System.Security.Claims;
using FacturAR.Web.Services;
using Microsoft.AspNetCore.Components.Authorization;

namespace FacturAR.Web.Auth;

public sealed class ApiAuthenticationStateProvider : AuthenticationStateProvider
{
    private static readonly AuthenticationState Anonimo =
        new(new ClaimsPrincipal(new ClaimsIdentity()));

    private readonly FacturarApiClient _api;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private AuthenticationState? _cache;

    public ApiAuthenticationStateProvider(
        FacturarApiClient api,
        IHttpContextAccessor httpContextAccessor)
    {
        _api = api;
        _httpContextAccessor = httpContextAccessor;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (_cache is not null)
        {
            return _cache;
        }

        var cookie = _httpContextAccessor.HttpContext?.Request.Headers.Cookie.ToString();
        if (string.IsNullOrWhiteSpace(cookie))
        {
            return _cache = Anonimo;
        }

        try
        {
            var empresa = await _api.GetMeEmpresaAsync(cookie);
            if (empresa is not null)
            {
                return _cache = Construir(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, empresa.UsuarioId.ToString()),
                    new Claim(ClaimTypes.Name, empresa.Email),
                    new Claim("email", empresa.Email),
                    new Claim("rol", empresa.Rol),
                    new Claim("contribuyenteId", empresa.ContribuyenteId.ToString()),
                    new Claim("plano", "Empresa")
                ], authenticationType: "cookies.contribuyente"));
            }

            var admin = await _api.GetMeAdminAsync(cookie);
            if (admin is not null)
            {
                return _cache = Construir(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, admin.AdministradorId.ToString()),
                    new Claim(ClaimTypes.Name, admin.Email),
                    new Claim("email", admin.Email),
                    new Claim("rolGlobal", admin.RolGlobal),
                    new Claim("plano", "Administrador")
                ], authenticationType: "cookies.admin"));
            }
        }
        catch (HttpRequestException)
        {
        }

        return _cache = Anonimo;
    }

    public void Invalidar()
    {
        _cache = null;
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    private static AuthenticationState Construir(ClaimsIdentity identity)
        => new(new ClaimsPrincipal(identity));
}