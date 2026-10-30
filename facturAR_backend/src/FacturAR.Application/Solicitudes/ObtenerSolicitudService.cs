using FacturAR.Application.Abstractions;
using FacturAR.Contracts.Solicitudes;
using FacturAR.Domain.Enums;
using FacturAR.Domain.Exceptions;

namespace FacturAR.Application.Solicitudes;

public sealed class ObtenerSolicitudService
{
    private readonly ISolicitudAltaRepository _solicitudes;
    private readonly IInvitacionRepository _invitaciones;
    private readonly IEventoAuditoriaRepository _eventos;

    public ObtenerSolicitudService(
        ISolicitudAltaRepository solicitudes,
        IInvitacionRepository invitaciones,
        IEventoAuditoriaRepository eventos)
    {
        _solicitudes = solicitudes;
        _invitaciones = invitaciones;
        _eventos = eventos;
    }

    public async Task<SolicitudAltaDetalleResponse> ObtenerAsync(Guid solicitudId, CancellationToken ct = default)
    {
        var solicitud = await _solicitudes.ObtenerPorIdAsync(solicitudId, ct)
            ?? throw new SolicitudNoEncontradaException();

        InvitacionResumenResponse? invitacion = null;
        if (solicitud.Estado == EstadoSolicitud.Aprobada)
        {
            var vigente = await _invitaciones.ObtenerVigentePorSolicitudAsync(solicitud.Id, ct);
            if (vigente is not null)
            {
                invitacion = new InvitacionResumenResponse(
                    vigente.Id,
                    vigente.EstadoEfectivo(DateTime.UtcNow).ToString(),
                    vigente.Email,
                    vigente.TokenPrefix,
                    vigente.ExpiraAt,
                    vigente.UltimoEnvioAt,
                    vigente.IntentosEnvio,
                    PuedeReenviarse: true);
            }
        }

        var historial = await _eventos.ListarPorEntidadAsync(
            NotificadorRevisionSolicitudes.Entidad,
            solicitud.Id.ToString(),
            ct);

        return new SolicitudAltaDetalleResponse(
            solicitud.Id,
            solicitud.Estado.ToString(),
            solicitud.Cuit,
            solicitud.RazonSocial,
            (int)solicitud.CondicionIva,
            solicitud.NombreSolicitante,
            solicitud.EmailSolicitante,
            solicitud.Telefono,
            solicitud.Domicilio,
            solicitud.Ciudad,
            solicitud.Provincia,
            solicitud.CodigoPostal,
            solicitud.Mensaje,
            solicitud.MotivoRechazo,
            solicitud.ContribuyenteId,
            solicitud.RevisadaPorAdministradorId,
            solicitud.RevisadaAt,
            solicitud.CreatedAt,
            solicitud.UpdatedAt,
            invitacion,
            historial
                .Select(e => new EventoRevisionResponse(e.Accion, e.ActorTipo, e.AdministradorId, e.CreatedAt))
                .ToList());
    }
}
