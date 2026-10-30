using FacturAR.Application.Solicitudes;
using FacturAR.Contracts.Comunes;
using FacturAR.Contracts.Solicitudes;
using FacturAR.Domain.Exceptions;
using MongoDB.Driver;

namespace FacturAR.Api.Solicitudes;

public static class SolicitudAltaEndpoints
{
    private const string MensajeMongoNoDisponible = "No pudimos registrar la solicitud. Intentá de nuevo en unos minutos.";

    public static IEndpointRouteBuilder MapSolicitudAltaEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/v1/solicitudes-alta", CrearAsync)
            .WithName("CrearSolicitudAlta")
            .WithTags("Solicitudes")
            .Produces<SolicitudAltaResponse>(StatusCodes.Status202Accepted)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status409Conflict)
            .Produces<ErrorResponse>(StatusCodes.Status429TooManyRequests)
            .Produces<ErrorResponse>(StatusCodes.Status500InternalServerError)
            .Produces<ErrorResponse>(StatusCodes.Status503ServiceUnavailable);

        return app;
    }

    private static async Task<IResult> CrearAsync(
        SolicitudAltaRequest request,
        CrearSolicitudAltaService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var respuesta = await service.CrearAsync(request, cancellationToken);
            return Results.Json(respuesta, statusCode: StatusCodes.Status202Accepted);
        }
        catch (SolicitudAltaValidationException ex)
        {
            return Results.BadRequest(new ErrorResponse("VALIDACION", ex.Message));
        }
        catch (SolicitudPendienteException ex)
        {
            return Results.Json(
                new ErrorResponse("SOLICITUD_PENDIENTE", ex.Message),
                statusCode: StatusCodes.Status409Conflict);
        }
        catch (Exception ex) when (ex is MongoException or TimeoutException)
        {
            return Results.Json(
                new ErrorResponse("MONGO_NO_DISPONIBLE", MensajeMongoNoDisponible),
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }
}
