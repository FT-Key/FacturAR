using FacturAR.Application.Abstractions;
using FacturAR.Contracts.Comunes;
using FacturAR.Contracts.PuntosVenta;
using MongoDB.Driver;

namespace FacturAR.Api.PuntosVenta;

public static class PuntoVentaEndpoint
{
    private const string MensajeMongoNoDisponible = "No hay conexión con la base de datos. Intentá de nuevo en unos minutos.";
    private const string CategoriaLogger = "FacturAR.Api.PuntosVenta";

    public static IEndpointRouteBuilder MapPuntoVentaEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/v1/puntos-venta", ListarAsync)
            .WithName("ListarPuntosVenta")
            .WithTags("Puntos de venta")
            .WithSummary("Lista los puntos de venta del contribuyente autenticado.")
            .WithDescription("Devuelve todos los puntos de venta de la empresa, activos e inactivos, ordenados por número.")
            .RequireAuthorization("EmpresaEmisor")
            .Produces<PuntoVentaListResponse>()
            .Produces<ErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ErrorResponse>(StatusCodes.Status403Forbidden)
            .Produces<ErrorResponse>(StatusCodes.Status500InternalServerError)
            .Produces<ErrorResponse>(StatusCodes.Status503ServiceUnavailable);

        return app;
    }

    private static async Task<IResult> ListarAsync(
        ITenantContext tenant,
        IPuntoVentaRepository puntosVenta,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        if (tenant.ContribuyenteId is not Guid contribuyenteId)
        {
            return Error(
                StatusCodes.Status401Unauthorized,
                "CREDENCIALES_INVALIDAS",
                "La sesión no es válida o expiró.");
        }

        try
        {
            var puntos = await puntosVenta.ListarPorContribuyenteAsync(contribuyenteId, cancellationToken);

            return Results.Ok(new PuntoVentaListResponse(
                [.. puntos.Select(p => new PuntoVentaResumenResponse(
                    p.Id,
                    p.Numero,
                    p.Descripcion,
                    p.Activo))]));
        }
        catch (Exception ex) when (ex is MongoConnectionException or TimeoutException)
        {
            return Error(StatusCodes.Status503ServiceUnavailable, "MONGO_NO_DISPONIBLE", MensajeMongoNoDisponible);
        }
        catch (Exception ex)
        {
            loggerFactory
                .CreateLogger(CategoriaLogger)
                .LogError(ex, "Error inesperado al listar puntos de venta del contribuyente {ContribuyenteId}.", contribuyenteId);

            return Error(
                StatusCodes.Status500InternalServerError,
                "ERROR_INTERNO",
                "Ocurrió un error inesperado.");
        }
    }

    private static IResult Error(int statusCode, string codigo, string mensaje)
        => Results.Json(new ErrorResponse(codigo, mensaje), statusCode: statusCode);
}
