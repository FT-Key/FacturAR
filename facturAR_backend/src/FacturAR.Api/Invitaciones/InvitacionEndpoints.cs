using FacturAR.Application.Invitaciones;
using FacturAR.Contracts.Comunes;
using FacturAR.Contracts.Invitaciones;
using FacturAR.Domain.Exceptions;
using MongoDB.Driver;

namespace FacturAR.Api.Invitaciones;

public static class InvitacionEndpoints
{
    public const string PoliticaValidacion = "invitacion-validacion";
    public const string PoliticaActivacion = "invitacion-activacion";

    private const string MensajeMongoNoDisponible = "No pudimos procesar la activación. Intentá de nuevo en unos minutos.";

    public static IEndpointRouteBuilder MapInvitacionEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/v1/invitaciones").WithTags("Invitaciones");

        grupo.MapGet("/{token}", ValidarAsync)
            .WithName("ValidarInvitacion")
            .RequireRateLimiting(PoliticaValidacion)
            .Produces<InvitacionValidacionResponse>()
            .Produces<ErrorResponse>(StatusCodes.Status403Forbidden)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ErrorResponse>(StatusCodes.Status409Conflict)
            .Produces<ErrorResponse>(StatusCodes.Status410Gone)
            .Produces<ErrorResponse>(StatusCodes.Status429TooManyRequests)
            .Produces<ErrorResponse>(StatusCodes.Status503ServiceUnavailable);

        grupo.MapPost("/{token}/aceptar", AceptarAsync)
            .WithName("AceptarInvitacion")
            .RequireRateLimiting(PoliticaActivacion)
            .Produces<AceptarInvitacionResponse>()
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status403Forbidden)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ErrorResponse>(StatusCodes.Status409Conflict)
            .Produces<ErrorResponse>(StatusCodes.Status410Gone)
            .Produces<ErrorResponse>(StatusCodes.Status429TooManyRequests)
            .Produces<ErrorResponse>(StatusCodes.Status503ServiceUnavailable);

        return app;
    }

    private static Task<IResult> ValidarAsync(
        string token,
        ValidarInvitacionService service,
        CancellationToken cancellationToken)
        => EjecutarAsync(async () => Results.Ok(await service.ValidarAsync(token, cancellationToken)));

    private static Task<IResult> AceptarAsync(
        string token,
        AceptarInvitacionRequest request,
        AceptarInvitacionService service,
        CancellationToken cancellationToken)
        => EjecutarAsync(async () => Results.Ok(await service.AceptarAsync(token, request, cancellationToken)));

    private static async Task<IResult> EjecutarAsync(Func<Task<IResult>> operacion)
    {
        try
        {
            return await operacion();
        }
        catch (InvitacionNoEncontradaException ex)
        {
            return Error(StatusCodes.Status404NotFound, "INVITACION_NO_ENCONTRADA", ex.Message);
        }
        catch (InvitacionVencidaException ex)
        {
            return Error(StatusCodes.Status410Gone, "INVITACION_VENCIDA", ex.Message);
        }
        catch (InvitacionUsadaException ex)
        {
            return Error(StatusCodes.Status409Conflict, "INVITACION_USADA", ex.Message);
        }
        catch (CuentaInactivaException ex)
        {
            return Error(StatusCodes.Status403Forbidden, "CUENTA_INACTIVA", ex.Message);
        }
        catch (PasswordDebilException ex)
        {
            return Error(StatusCodes.Status400BadRequest, "PASSWORD_DEBIL", ex.Message);
        }
        catch (SolicitudAltaValidationException ex)
        {
            return Error(StatusCodes.Status400BadRequest, "VALIDACION", ex.Message);
        }
        catch (Exception ex) when (ex is MongoConnectionException or TimeoutException)
        {
            return Error(StatusCodes.Status503ServiceUnavailable, "MONGO_NO_DISPONIBLE", MensajeMongoNoDisponible);
        }
    }

    private static IResult Error(int statusCode, string codigo, string mensaje)
        => Results.Json(new ErrorResponse(codigo, mensaje), statusCode: statusCode);
}
