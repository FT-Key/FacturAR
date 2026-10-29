using FacturAR.Application.Abstractions;
using FacturAR.Application.Auth;
using FacturAR.Contracts.Comunes;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace FacturAR.Api.Auth;

public static class CookieAuthenticationExtensions
{
    public const string EmpresaScheme = "cookies.contribuyente";
    public const string AdminScheme = "cookies.admin";

    public static IServiceCollection AddFacturarCookies(
        this IServiceCollection services,
        AuthOptions options,
        bool isDevelopment)
    {
        var securePolicy = isDevelopment ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        var dominio = string.IsNullOrWhiteSpace(options.Cookie.Dominio) ? null : options.Cookie.Dominio;

        services
            .AddAuthentication(o =>
            {
                o.DefaultScheme = EmpresaScheme;
            })
            .AddCookie(EmpresaScheme, o =>
            {
                o.Cookie.Name = options.Cookie.NombreContribuyente;
                o.Cookie.HttpOnly = true;
                o.Cookie.SameSite = SameSiteMode.Lax;
                o.Cookie.SecurePolicy = securePolicy;
                o.Cookie.Path = "/";
                o.Cookie.Domain = dominio;
                o.ExpireTimeSpan = TimeSpan.FromHours(options.Cookie.HorasSesionEmpresa);
                o.SlidingExpiration = false;
                o.Events.OnRedirectToLogin = Problema(
                    StatusCodes.Status401Unauthorized,
                    "CREDENCIALES_INVALIDAS",
                    "La sesión no es válida o expiró.");
                o.Events.OnRedirectToAccessDenied = Problema(
                    StatusCodes.Status403Forbidden,
                    "SIN_PERMISOS",
                    "El rol del usuario no autoriza la operación.");
            })
            .AddCookie(AdminScheme, o =>
            {
                o.Cookie.Name = options.Cookie.NombreAdmin;
                o.Cookie.HttpOnly = true;
                o.Cookie.SameSite = SameSiteMode.Strict;
                o.Cookie.SecurePolicy = securePolicy;
                o.Cookie.Path = "/";
                o.Cookie.Domain = dominio;
                o.ExpireTimeSpan = TimeSpan.FromHours(options.Cookie.HorasSesionAdmin);
                o.SlidingExpiration = false;
                o.Events.OnRedirectToLogin = Problema(
                    StatusCodes.Status401Unauthorized,
                    "CREDENCIALES_INVALIDAS",
                    "La sesión no es válida o expiró.");
                o.Events.OnRedirectToAccessDenied = Problema(
                    StatusCodes.Status403Forbidden,
                    "SIN_PERMISOS",
                    "El rol del usuario no autoriza la operación.");
            });

        services.AddAuthorization(o =>
        {
            o.AddPolicy("EmpresaAutenticado", p => p
                .AddAuthenticationSchemes(EmpresaScheme, AdminScheme)
                .RequireAuthenticatedUser()
                .RequireClaim("contribuyenteId"));

            o.AddPolicy("EmpresaAdministrador", p => p
                .AddAuthenticationSchemes(EmpresaScheme, AdminScheme)
                .RequireAuthenticatedUser()
                .RequireClaim("contribuyenteId")
                .RequireClaim("rol", "Administrador"));

            o.AddPolicy("EmpresaEmisor", p => p
                .AddAuthenticationSchemes(EmpresaScheme, AdminScheme)
                .RequireAuthenticatedUser()
                .RequireClaim("contribuyenteId")
                .RequireClaim("rol", "Administrador", "Emisor"));

            o.AddPolicy("EmpresaConsulta", p => p
                .AddAuthenticationSchemes(EmpresaScheme, AdminScheme)
                .RequireAuthenticatedUser()
                .RequireClaim("contribuyenteId")
                .RequireClaim("rol", "Administrador", "Emisor", "Consulta"));

            o.AddPolicy("AdminGlobal", p => p
                .AddAuthenticationSchemes(EmpresaScheme, AdminScheme)
                .RequireAuthenticatedUser()
                .RequireClaim("rolGlobal", "SuperAdmin", "Soporte"));

            o.AddPolicy("AdminSuper", p => p
                .AddAuthenticationSchemes(EmpresaScheme, AdminScheme)
                .RequireAuthenticatedUser()
                .RequireClaim("rolGlobal", "SuperAdmin"));
        });

        return services;
    }

    private static Func<RedirectContext<CookieAuthenticationOptions>, Task> Problema(
        int statusCode,
        string codigo,
        string mensaje)
        => async context =>
        {
            if (context.Response.HasStarted)
            {
                return;
            }

            if (statusCode == StatusCodes.Status403Forbidden)
            {
                await AuditarAccesoDenegadoAsync(context);
            }

            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new ErrorResponse(codigo, mensaje));
        };

    private static async Task AuditarAccesoDenegadoAsync(RedirectContext<CookieAuthenticationOptions> context)
    {
        try
        {
            var services = context.HttpContext.RequestServices;
            var auditoria = services.GetService<RegistrarAccesoService>();
            if (auditoria is null)
            {
                return;
            }

            var tenant = services.GetService<ITenantContext>();
            await auditoria.RegistrarAccesoDenegadoAsync(
                tenant?.UsuarioId,
                tenant?.ContribuyenteId,
                tenant?.AdministradorId);
        }
        catch
        {
            // La auditoría nunca debe cambiar el resultado de la autorización.
        }
    }
}