using System.Security.Claims;
using FacturAR.Api.Auth;
using FacturAR.Application.Abstractions;
using FacturAR.Application.Comprobantes;
using FacturAR.Contracts.Comprobantes;
using FacturAR.Contracts.Comunes;
using FacturAR.Domain.Exceptions;
using MongoDB.Driver;

namespace FacturAR.Api.Comprobantes;

public static class ComprobanteConsultaEndpoints
{
    public const string Politica = "ComprobantesEmpresa";

    private const string ClaimRol = "rol";
    private const string RolAdministrador = "Administrador";
    private const string RolEmisor = "Emisor";
    private const string RolConsulta = "Consulta";
    private const string MensajeMongoNoDisponible = "No hay conexión con la base de datos. Intentá de nuevo en unos minutos.";
    internal const string CategoriaLogger = "FacturAR.Api.Comprobantes";

    public static IServiceCollection AddComprobanteConsultaAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options => options.AddPolicy(Politica, policy => policy
            .AddAuthenticationSchemes(
                ApiKeyAuthenticationHandler.SchemeName,
                CookieAuthenticationExtensions.EmpresaScheme)
            .RequireAuthenticatedUser()
            .RequireClaim(ApiKeyAuthenticationHandler.ClaimContribuyenteId)));

        return services;
    }

    public static IEndpointRouteBuilder MapComprobanteConsultaEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/v1/comprobantes", ListarAsync)
            .WithName("ListarComprobantes")
            .WithTags("Comprobantes")
            .WithSummary("Lista los comprobantes del contribuyente autenticado.")
            .WithDescription(
                "Filtra por rango de fechas UTC, tipo, estado, punto de venta y receptor. "
                + "Ordena por fecha de emisión y número, ambos descendentes, y pagina con page y pageSize.")
            .RequireAuthorization(Politica)
            .Produces<PaginaResponse<ComprobanteResumenResponse>>()
            .ConErroresComunes();

        app.MapGet("/v1/comprobantes/ultimo-autorizado", ObtenerUltimoAutorizadoAsync)
            .WithName("ObtenerUltimoAutorizado")
            .WithTags("Comprobantes")
            .WithSummary("Devuelve el último número autorizado por tipo y punto de venta.")
            .WithDescription(
                "El parámetro tipo es obligatorio. Sin puntoVenta, la respuesta agrega por tipo y "
                + "devuelve puntoVenta nulo. Si no hay secuencia, el número es 0.")
            .RequireAuthorization(Politica)
            .Produces<UltimoAutorizadoResponse>()
            .ConErroresComunes();

        app.MapGet("/v1/comprobantes/{id:guid}", ObtenerDetalleAsync)
            .WithName("ConsultarComprobante")
            .WithTags("Comprobantes")
            .WithSummary("Devuelve el detalle de un comprobante propio, con sus ítems.")
            .RequireAuthorization(Politica)
            .Produces<ComprobanteDetalleResponse>()
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
            .ConErroresComunes();

        app.MapGet("/v1/comprobantes/{id}", IdentificadorInvalido)
            .ExcludeFromDescription()
            .RequireAuthorization(Politica)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest);

        return app;
    }

    private static Task<IResult> ListarAsync(
        int? page,
        int? pageSize,
        string? desde,
        string? hasta,
        int? tipo,
        int? estado,
        int? puntoVenta,
        string? receptor,
        HttpContext httpContext,
        ITenantContext tenant,
        ListarComprobantesService service,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        if (ValidarAcceso(httpContext, tenant, out var contribuyenteId) is IResult error)
        {
            return Task.FromResult(error);
        }

        return EjecutarAsync(
            loggerFactory.CreateLogger(CategoriaLogger),
            contribuyenteId,
            async () => Results.Ok(await service.ListarAsync(
                contribuyenteId,
                page,
                pageSize,
                desde,
                hasta,
                tipo,
                estado,
                puntoVenta,
                receptor,
                cancellationToken)));
    }

    private static Task<IResult> ObtenerDetalleAsync(
        Guid id,
        HttpContext httpContext,
        ITenantContext tenant,
        ConsultarComprobanteService service,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        if (ValidarAcceso(httpContext, tenant, out var contribuyenteId) is IResult error)
        {
            return Task.FromResult(error);
        }

        return EjecutarAsync(
            loggerFactory.CreateLogger(CategoriaLogger),
            contribuyenteId,
            async () =>
            {
                var detalle = await service.ConsultarAsync(contribuyenteId, id, cancellationToken);

                return detalle is null
                    ? Error(
                        StatusCodes.Status404NotFound,
                        "COMPROBANTE_NO_ENCONTRADO",
                        "No existe un comprobante con el id indicado.")
                    : Results.Ok(detalle);
            });
    }

    private static Task<IResult> ObtenerUltimoAutorizadoAsync(
        int? tipo,
        int? puntoVenta,
        HttpContext httpContext,
        ITenantContext tenant,
        ObtenerUltimoAutorizadoService service,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        if (ValidarAcceso(httpContext, tenant, out var contribuyenteId) is IResult error)
        {
            return Task.FromResult(error);
        }

        return EjecutarAsync(
            loggerFactory.CreateLogger(CategoriaLogger),
            contribuyenteId,
            async () => Results.Ok(await service.ObtenerAsync(
                contribuyenteId,
                tipo,
                puntoVenta,
                cancellationToken)));
    }

    private static IResult IdentificadorInvalido()
        => Error(
            StatusCodes.Status400BadRequest,
            "VALIDACION",
            "El identificador del comprobante no es válido.");

    internal static IResult? ValidarAcceso(
        HttpContext httpContext,
        ITenantContext tenant,
        out Guid contribuyenteId)
    {
        contribuyenteId = Guid.Empty;

        if (tenant.ContribuyenteId is not Guid id)
        {
            return Error(StatusCodes.Status401Unauthorized, "API_KEY_INVALIDA", "La API key no es válida.");
        }

        contribuyenteId = id;
        return DenegarPorRol(httpContext.User);
    }

    private static IResult? DenegarPorRol(ClaimsPrincipal user)
    {
        if (user.FindFirst(ApiKeyAuthenticationHandler.ClaimApiKeyId) is not null)
        {
            return null;
        }

        var rol = user.FindFirst(ClaimRol)?.Value;

        return rol is RolAdministrador or RolEmisor or RolConsulta
            ? null
            : Error(
                StatusCodes.Status403Forbidden,
                "SIN_PERMISOS",
                "El rol del usuario no autoriza la operación.");
    }

    internal static async Task<IResult> EjecutarAsync(
        ILogger logger,
        Guid contribuyenteId,
        Func<Task<IResult>> operacion)
    {
        try
        {
            return await operacion();
        }
        catch (ComprobanteValidationException ex)
        {
            return Error(StatusCodes.Status400BadRequest, "VALIDACION", ex.Message);
        }
        catch (Exception ex) when (ex is MongoConnectionException or TimeoutException)
        {
            return Error(StatusCodes.Status503ServiceUnavailable, "MONGO_NO_DISPONIBLE", MensajeMongoNoDisponible);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Error inesperado al consultar comprobantes del contribuyente {ContribuyenteId}.",
                contribuyenteId);

            return Error(
                StatusCodes.Status500InternalServerError,
                "ERROR_INTERNO",
                "Ocurrió un error inesperado.");
        }
    }

    internal static RouteHandlerBuilder ConErroresComunes(this RouteHandlerBuilder builder)
        => builder
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ErrorResponse>(StatusCodes.Status403Forbidden)
            .Produces<ErrorResponse>(StatusCodes.Status500InternalServerError)
            .Produces<ErrorResponse>(StatusCodes.Status503ServiceUnavailable);

    internal static IResult Error(int statusCode, string codigo, string mensaje)
        => Results.Json(new ErrorResponse(codigo, mensaje), statusCode: statusCode);
}
