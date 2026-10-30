using FacturAR.Application.Abstractions;
using FacturAR.Application.Solicitudes;
using FacturAR.Contracts.Comunes;
using FacturAR.Contracts.Solicitudes;
using FacturAR.Domain.Exceptions;
using MongoDB.Driver;

namespace FacturAR.Api.Solicitudes;

public static class SolicitudesAdminEndpoints
{
    private const string MensajeSinPermisos = "No tenés permisos para realizar esta acción.";
    private const string MensajeEmailNoEnviado = "No se pudo enviar el email. Podés reenviar la invitación.";
    private const string MensajeMongoNoDisponible = "No hay conexión con la base de datos. Intentá de nuevo en unos minutos.";

    public static IEndpointRouteBuilder MapSolicitudesAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/v1/admin/solicitudes")
            .WithTags("Admin")
            .RequireAuthorization("AdminGlobal");

        grupo.MapGet("/", ListarAsync)
            .WithName("ListarSolicitudesAlta")
            .ConErroresComunes()
            .Produces<PaginaResponse<SolicitudAltaResumenResponse>>()
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest);

        grupo.MapGet("/{id:guid}", ObtenerAsync)
            .WithName("ObtenerSolicitudAlta")
            .ConErroresComunes()
            .Produces<SolicitudAltaDetalleResponse>()
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        grupo.MapPut("/{id:guid}", EditarAsync)
            .WithName("EditarSolicitudAlta")
            .ConErroresComunes()
            .RequireAuthorization("AdminSuper")
            .Produces<SolicitudAltaDetalleResponse>()
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ErrorResponse>(StatusCodes.Status409Conflict);

        grupo.MapPost("/{id:guid}/aprobar", AprobarAsync)
            .WithName("AprobarSolicitudAlta")
            .ConErroresComunes()
            .RequireAuthorization("AdminSuper")
            .Produces<AprobarSolicitudResponse>()
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ErrorResponse>(StatusCodes.Status409Conflict);

        grupo.MapPost("/{id:guid}/rechazar", RechazarAsync)
            .WithName("RechazarSolicitudAlta")
            .ConErroresComunes()
            .RequireAuthorization("AdminSuper")
            .Produces<RechazarSolicitudResponse>()
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ErrorResponse>(StatusCodes.Status409Conflict);

        grupo.MapPost("/{id:guid}/reenviar-invitacion", ReenviarInvitacionAsync)
            .WithName("ReenviarInvitacion")
            .ConErroresComunes()
            .Produces<ReenviarInvitacionResponse>()
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ErrorResponse>(StatusCodes.Status502BadGateway);

        return app;
    }

    private static Task<IResult> ListarAsync(
        int? page,
        int? pageSize,
        string? estado,
        string? cuit,
        string? razonSocial,
        string? email,
        ListarSolicitudesService service,
        CancellationToken cancellationToken)
        => EjecutarAsync(async () => Results.Ok(
            await service.ListarAsync(page, pageSize, estado, cuit, razonSocial, email, cancellationToken)));

    private static Task<IResult> ObtenerAsync(
        Guid id,
        ObtenerSolicitudService service,
        CancellationToken cancellationToken)
        => EjecutarAsync(async () => Results.Ok(await service.ObtenerAsync(id, cancellationToken)));

    private static Task<IResult> EditarAsync(
        Guid id,
        EditarSolicitudRequest request,
        ITenantContext tenant,
        EditarSolicitudService editar,
        ObtenerSolicitudService obtener,
        CancellationToken cancellationToken)
        => EjecutarConAdministradorAsync(tenant, async administradorId =>
        {
            await editar.EditarAsync(id, request, administradorId, cancellationToken);
            return Results.Ok(await obtener.ObtenerAsync(id, cancellationToken));
        });

    private static Task<IResult> AprobarAsync(
        Guid id,
        ITenantContext tenant,
        AprobarSolicitudService service,
        CancellationToken cancellationToken)
        => EjecutarConAdministradorAsync(tenant, async administradorId =>
            Results.Ok(await service.AprobarAsync(id, administradorId, cancellationToken)));

    private static Task<IResult> RechazarAsync(
        Guid id,
        RechazarSolicitudRequest request,
        ITenantContext tenant,
        RechazarSolicitudService service,
        CancellationToken cancellationToken)
        => EjecutarConAdministradorAsync(tenant, async administradorId =>
            Results.Ok(await service.RechazarAsync(id, request.Motivo, administradorId, cancellationToken)));

    private static Task<IResult> ReenviarInvitacionAsync(
        Guid id,
        ITenantContext tenant,
        ReenviarInvitacionService service,
        CancellationToken cancellationToken)
        => EjecutarConAdministradorAsync(tenant, async administradorId =>
        {
            var respuesta = await service.ReenviarAsync(id, administradorId, cancellationToken);
            return respuesta.EmailEnviado
                ? Results.Ok(respuesta)
                : Error(StatusCodes.Status502BadGateway, "EMAIL_NO_ENVIADO", MensajeEmailNoEnviado);
        });

    private static Task<IResult> EjecutarConAdministradorAsync(
        ITenantContext tenant,
        Func<Guid, Task<IResult>> operacion)
        => tenant.AdministradorId is Guid administradorId
            ? EjecutarAsync(() => operacion(administradorId))
            : Task.FromResult(Error(StatusCodes.Status403Forbidden, "SIN_PERMISOS", MensajeSinPermisos));

    private static async Task<IResult> EjecutarAsync(Func<Task<IResult>> operacion)
    {
        try
        {
            return await operacion();
        }
        catch (SolicitudAltaValidationException ex)
        {
            return Error(StatusCodes.Status400BadRequest, "VALIDACION", ex.Message);
        }
        catch (SolicitudNoEncontradaException ex)
        {
            return Error(StatusCodes.Status404NotFound, "SOLICITUD_NO_ENCONTRADA", ex.Message);
        }
        catch (InvitacionNoEncontradaException ex)
        {
            return Error(StatusCodes.Status404NotFound, "INVITACION_NO_ENCONTRADA", ex.Message);
        }
        catch (SolicitudYaProcesadaException ex)
        {
            return Error(StatusCodes.Status409Conflict, "SOLICITUD_YA_PROCESADA", ex.Message);
        }
        catch (CuitYaRegistradoException ex)
        {
            return Error(StatusCodes.Status409Conflict, "CUIT_YA_REGISTRADO", ex.Message);
        }
        catch (SolicitudPendienteException ex)
        {
            return Error(StatusCodes.Status409Conflict, "SOLICITUD_PENDIENTE", ex.Message);
        }
        catch (Exception ex) when (ex is MongoConnectionException or TimeoutException)
        {
            return Error(StatusCodes.Status503ServiceUnavailable, "MONGO_NO_DISPONIBLE", MensajeMongoNoDisponible);
        }
    }

    private static RouteHandlerBuilder ConErroresComunes(this RouteHandlerBuilder builder)
        => builder
            .Produces<ErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ErrorResponse>(StatusCodes.Status403Forbidden)
            .Produces<ErrorResponse>(StatusCodes.Status503ServiceUnavailable);

    private static IResult Error(int statusCode, string codigo, string mensaje)
        => Results.Json(new ErrorResponse(codigo, mensaje), statusCode: statusCode);
}
