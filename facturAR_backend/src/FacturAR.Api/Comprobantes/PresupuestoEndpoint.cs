using FacturAR.Application.Abstractions;
using FacturAR.Application.Comprobantes;
using FacturAR.Contracts.Comprobantes;
using FacturAR.Contracts.Comunes;
using FacturAR.Domain.Exceptions;
using MongoDB.Driver;

namespace FacturAR.Api.Comprobantes;

public static class PresupuestoEndpoint
{
    private const string MensajeMongoNoDisponible = "No hay conexión con la base de datos. Intentá de nuevo en unos minutos.";
    private const string CategoriaLogger = "FacturAR.Api.Comprobantes";

    public static IEndpointRouteBuilder MapPresupuestoEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/v1/comprobantes/presupuesto", CalcularAsync)
            .WithName("CalcularPresupuesto")
            .WithTags("Comprobantes")
            .WithSummary("Calcula los importes de un comprobante sin emitirlo.")
            .WithDescription(
                "Valida el payload con las mismas reglas que la emisión y devuelve neto, IVA, total e ítems. "
                + "No persiste, no consume número, no genera CAE y no audita.")
            .RequireAuthorization("EmpresaEmisor")
            .Produces<PresupuestoResponse>()
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ErrorResponse>(StatusCodes.Status403Forbidden)
            .Produces<ErrorResponse>(StatusCodes.Status500InternalServerError)
            .Produces<ErrorResponse>(StatusCodes.Status503ServiceUnavailable);

        return app;
    }

    private static async Task<IResult> CalcularAsync(
        EmitirComprobanteRequest request,
        ITenantContext tenant,
        CalcularPresupuestoService service,
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
            return Results.Ok(await service.CalcularAsync(contribuyenteId, request, cancellationToken));
        }
        catch (ComprobanteValidationException ex)
        {
            return Error(StatusCodes.Status400BadRequest, "VALIDACION", ex.Message);
        }
        catch (EmisionInvalidaException ex)
        {
            return Error(StatusCodes.Status400BadRequest, "EMISION_INVALIDA", ex.Message);
        }
        catch (CuitInvalidoException ex)
        {
            return Error(StatusCodes.Status400BadRequest, "CUIT_INVALIDO", ex.Message);
        }
        catch (SinPermisosException ex)
        {
            return Error(StatusCodes.Status403Forbidden, "SIN_PERMISOS", ex.Message);
        }
        catch (Exception ex) when (ex is MongoConnectionException or TimeoutException)
        {
            return Error(StatusCodes.Status503ServiceUnavailable, "MONGO_NO_DISPONIBLE", MensajeMongoNoDisponible);
        }
        catch (Exception ex)
        {
            loggerFactory
                .CreateLogger(CategoriaLogger)
                .LogError(ex, "Error inesperado al calcular un presupuesto para el contribuyente {ContribuyenteId}.", contribuyenteId);

            return Error(
                StatusCodes.Status500InternalServerError,
                "ERROR_INTERNO",
                "Ocurrió un error inesperado.");
        }
    }

    private static IResult Error(int statusCode, string codigo, string mensaje)
        => Results.Json(new ErrorResponse(codigo, mensaje), statusCode: statusCode);
}
