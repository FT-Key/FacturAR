using System.Security.Claims;
using FacturAR.Application.Abstractions;
using FacturAR.Application.Auth;
using FacturAR.Contracts.Auth;
using FacturAR.Contracts.Comunes;
using Microsoft.AspNetCore.Authentication;

namespace FacturAR.Api.Auth;

public static class AuthEndpoints
{
    private static readonly IResult CredencialesInvalidas = Results.Json(
        new ErrorResponse("CREDENCIALES_INVALIDAS", "Email o contraseña incorrectos."),
        statusCode: StatusCodes.Status401Unauthorized);

    private static readonly IResult ValidacionInvalida = Results.BadRequest(
        new ErrorResponse("VALIDACION", "El email y la contraseña son obligatorios y deben cumplir el formato esperado."));

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var empresa = app.MapGroup("/v1/auth").WithTags("Autenticación");

        empresa.MapPost("/login", LoginEmpresaAsync)
            .WithName("LoginEmpresa")
            .RequireRateLimiting("login")
            .Produces<SesionEmpresaResponse>()
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ErrorResponse>(StatusCodes.Status429TooManyRequests);

        empresa.MapPost("/logout", LogoutEmpresaAsync)
            .WithName("LogoutEmpresa")
            .RequireAuthorization("EmpresaAutenticado")
            .Produces(StatusCodes.Status204NoContent);

        empresa.MapGet("/me", GetMeEmpresaAsync)
            .WithName("GetMeEmpresa")
            .RequireAuthorization("EmpresaAutenticado")
            .Produces<SesionEmpresaResponse>()
            .Produces<ErrorResponse>(StatusCodes.Status401Unauthorized);

        var admin = app.MapGroup("/v1/admin/auth").WithTags("Admin");

        admin.MapPost("/login", LoginAdminGlobalAsync)
            .WithName("LoginAdminGlobal")
            .RequireRateLimiting("login")
            .Produces<SesionAdminResponse>()
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ErrorResponse>(StatusCodes.Status429TooManyRequests);

        admin.MapPost("/logout", LogoutAdminGlobalAsync)
            .WithName("LogoutAdminGlobal")
            .RequireAuthorization("AdminGlobal")
            .Produces(StatusCodes.Status204NoContent);

        admin.MapGet("/me", GetMeAdminAsync)
            .WithName("GetMeAdmin")
            .RequireAuthorization("AdminGlobal")
            .Produces<SesionAdminResponse>()
            .Produces<ErrorResponse>(StatusCodes.Status401Unauthorized);

        return app;
    }

    private static async Task<IResult> LoginEmpresaAsync(
        LoginRequest request,
        AutenticarEmpresaService service,
        RegistrarAccesoService auditoria,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        if (!EsValido(request))
        {
            return ValidacionInvalida;
        }

        var sesion = await service.AutenticarAsync(request.Email, request.Password, cancellationToken);
        if (sesion is null)
        {
            await auditoria.RegistrarLoginFallidoAsync(request.Email, cancellationToken);
            return CredencialesInvalidas;
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, sesion.UsuarioId.ToString()),
            new Claim("email", sesion.Email),
            new Claim("rol", sesion.Rol),
            new Claim("contribuyenteId", sesion.ContribuyenteId.ToString()),
            new Claim("plano", "Empresa")
        ], CookieAuthenticationExtensions.EmpresaScheme));

        await http.SignInAsync(
            CookieAuthenticationExtensions.EmpresaScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = sesion.ExpiraAtUtc,
                AllowRefresh = false
            });

        await auditoria.RegistrarLoginEmpresaAsync(sesion.UsuarioId, sesion.ContribuyenteId, cancellationToken);
        return Results.Ok(sesion);
    }

    private static async Task<IResult> LogoutEmpresaAsync(
        HttpContext http,
        ITenantContext tenant,
        RegistrarAccesoService auditoria,
        CancellationToken cancellationToken)
    {
        if (tenant.UsuarioId is Guid usuarioId && tenant.ContribuyenteId is Guid contribuyenteId)
        {
            await auditoria.RegistrarLogoutEmpresaAsync(usuarioId, contribuyenteId, cancellationToken);
        }

        await http.SignOutAsync(CookieAuthenticationExtensions.EmpresaScheme);
        return Results.NoContent();
    }

    private static async Task<IResult> GetMeEmpresaAsync(
        HttpContext http,
        ITenantContext tenant,
        IContribuyenteRepository contribuyentes,
        AuthOptions options,
        CancellationToken cancellationToken)
    {
        var auth = await http.AuthenticateAsync(CookieAuthenticationExtensions.EmpresaScheme);
        if (!auth.Succeeded || tenant.UsuarioId is null || tenant.ContribuyenteId is null)
        {
            return CredencialesInvalidas;
        }

        var contribuyente = await contribuyentes.GetByIdAsync(tenant.ContribuyenteId.Value, cancellationToken);

        return Results.Ok(new SesionEmpresaResponse(
            tenant.UsuarioId.Value,
            auth.Principal?.FindFirst("email")?.Value ?? string.Empty,
            auth.Principal?.FindFirst("rol")?.Value ?? string.Empty,
            tenant.ContribuyenteId.Value,
            contribuyente?.RazonSocial ?? string.Empty,
            auth.Properties?.ExpiresUtc?.UtcDateTime ?? DateTime.UtcNow.AddHours(options.Cookie.HorasSesionEmpresa)));
    }

    private static async Task<IResult> LoginAdminGlobalAsync(
        LoginRequest request,
        AutenticarAdminGlobalService service,
        RegistrarAccesoService auditoria,
        HttpContext http,
        CancellationToken cancellationToken)
    {
        if (!EsValido(request))
        {
            return ValidacionInvalida;
        }

        var sesion = await service.AutenticarAsync(request.Email, request.Password, cancellationToken);
        if (sesion is null)
        {
            await auditoria.RegistrarLoginFallidoAsync(request.Email, cancellationToken);
            return CredencialesInvalidas;
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, sesion.AdministradorId.ToString()),
            new Claim("email", sesion.Email),
            new Claim("rolGlobal", sesion.RolGlobal),
            new Claim("plano", "Administrador")
        ], CookieAuthenticationExtensions.AdminScheme));

        await http.SignInAsync(
            CookieAuthenticationExtensions.AdminScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = sesion.ExpiraAtUtc,
                AllowRefresh = false
            });

        await auditoria.RegistrarLoginAdminGlobalAsync(sesion.AdministradorId, cancellationToken);
        return Results.Ok(sesion);
    }

    private static async Task<IResult> LogoutAdminGlobalAsync(
        HttpContext http,
        ITenantContext tenant,
        RegistrarAccesoService auditoria,
        CancellationToken cancellationToken)
    {
        if (tenant.AdministradorId is Guid administradorId)
        {
            await auditoria.RegistrarLogoutAdminGlobalAsync(administradorId, cancellationToken);
        }

        await http.SignOutAsync(CookieAuthenticationExtensions.AdminScheme);
        return Results.NoContent();
    }

    private static async Task<IResult> GetMeAdminAsync(
        HttpContext http,
        ITenantContext tenant,
        IAdministradorRepository administradores,
        AuthOptions options,
        CancellationToken cancellationToken)
    {
        var auth = await http.AuthenticateAsync(CookieAuthenticationExtensions.AdminScheme);
        if (!auth.Succeeded || tenant.AdministradorId is null)
        {
            return CredencialesInvalidas;
        }

        var administrador = await administradores.GetByIdAsync(tenant.AdministradorId.Value, cancellationToken);

        return Results.Ok(new SesionAdminResponse(
            tenant.AdministradorId.Value,
            auth.Principal?.FindFirst("email")?.Value ?? string.Empty,
            administrador?.Nombre ?? string.Empty,
            auth.Principal?.FindFirst("rolGlobal")?.Value ?? string.Empty,
            auth.Properties?.ExpiresUtc?.UtcDateTime ?? DateTime.UtcNow.AddHours(options.Cookie.HorasSesionAdmin)));
    }

    private static bool EsValido(LoginRequest request)
    {
        var email = request.Email?.Trim();
        return !string.IsNullOrEmpty(email)
            && email.Length <= 256
            && !string.IsNullOrEmpty(request.Password)
            && request.Password.Length <= 128;
    }
}