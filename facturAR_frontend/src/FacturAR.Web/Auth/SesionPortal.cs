using System.Security.Claims;
using Microsoft.Net.Http.Headers;

namespace FacturAR.Web.Auth;

public static class SesionPortal
{
    public const string NombreAdminPorDefecto = ".FacturAR.Admin";
    public const string NombreContribuyentePorDefecto = ".FacturAR.Contribuyente";
    public const string CookieTema = "facturar-tema";

    public static string NombreAdmin(IConfiguration configuration)
        => configuration["Auth:Cookie:NombreAdmin"] ?? NombreAdminPorDefecto;

    public static string NombreContribuyente(IConfiguration configuration)
        => configuration["Auth:Cookie:NombreContribuyente"] ?? NombreContribuyentePorDefecto;

    public static bool EsRutaAdmin(PathString path)
        => path.StartsWithSegments("/admin", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/v1/admin", StringComparison.OrdinalIgnoreCase);

    public static string PanelUrl(ClaimsPrincipal usuario)
        => usuario.HasClaim("plano", "Administrador") ? "/admin" : "/dashboard";

    public static void EliminarCookiesDeSesion(
        HttpResponse response,
        IConfiguration configuration,
        params string[] nombres)
    {
        var dominio = configuration["Auth:Cookie:Dominio"];
        var seguro = response.HttpContext.Request.IsHttps ? "; secure" : string.Empty;

        foreach (var nombre in nombres.Distinct(StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(nombre))
            {
                continue;
            }

            response.Headers.Append(
                HeaderNames.SetCookie,
                $"{nombre}=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/; samesite=lax{seguro}");

            if (!string.IsNullOrWhiteSpace(dominio))
            {
                response.Headers.Append(
                    HeaderNames.SetCookie,
                    $"{nombre}=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/; samesite=lax; domain={dominio}{seguro}");
            }
        }
    }
}
