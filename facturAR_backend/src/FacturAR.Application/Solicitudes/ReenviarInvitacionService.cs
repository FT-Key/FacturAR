using FacturAR.Application.Abstractions;
using FacturAR.Application.Notificaciones;
using FacturAR.Contracts.Solicitudes;
using FacturAR.Domain.Entities;
using FacturAR.Domain.Enums;
using FacturAR.Domain.Exceptions;
using FacturAR.Domain.ValueObjects;

namespace FacturAR.Application.Solicitudes;

public sealed class ReenviarInvitacionService
{
    public const string AdvertenciaEmail = "No se pudo enviar el email. Podés reenviar la invitación.";
    public const string AccionReenviada = "invitacion.reenviada";

    private readonly ISolicitudAltaRepository _solicitudes;
    private readonly IInvitacionRepository _invitaciones;
    private readonly IEventoAuditoriaRepository _eventos;
    private readonly ITransaccionMongo _transaccion;
    private readonly EmailService _email;
    private readonly NotificadorRevisionSolicitudes _notificador;

    public ReenviarInvitacionService(
        ISolicitudAltaRepository solicitudes,
        IInvitacionRepository invitaciones,
        IEventoAuditoriaRepository eventos,
        ITransaccionMongo transaccion,
        EmailService email,
        NotificadorRevisionSolicitudes notificador)
    {
        _solicitudes = solicitudes;
        _invitaciones = invitaciones;
        _eventos = eventos;
        _transaccion = transaccion;
        _email = email;
        _notificador = notificador;
    }

    public async Task<ReenviarInvitacionResponse> ReenviarAsync(
        Guid solicitudId,
        Guid administradorId,
        CancellationToken ct = default)
    {
        var solicitud = await _solicitudes.ObtenerPorIdAsync(solicitudId, ct)
            ?? throw new SolicitudNoEncontradaException();

        if (solicitud.Estado != EstadoSolicitud.Aprobada)
        {
            throw new InvitacionNoEncontradaException();
        }

        var token = TokenInvitacion.Generar(DateTime.UtcNow);

        var nueva = await _transaccion.EjecutarAsync(
            async innerCt =>
            {
                var anterior = await _invitaciones.ObtenerVigentePorSolicitudAsync(solicitudId, innerCt)
                    ?? throw new InvitacionNoEncontradaException();

                var ahora = DateTime.UtcNow;
                if (!await _invitaciones.RevocarAsync(anterior.Id, ahora, innerCt))
                {
                    throw new InvitacionNoEncontradaException();
                }

                var invitacion = new Invitacion
                {
                    ContribuyenteId = anterior.ContribuyenteId,
                    SolicitudId = solicitudId,
                    Email = anterior.Email,
                    Rol = anterior.Rol,
                    TokenHash = token.Hash,
                    TokenPrefix = token.Prefix,
                    Estado = EstadoInvitacion.Pendiente,
                    ExpiraAt = token.ExpiraAt,
                    CreadaPorAdministradorId = administradorId,
                    IntentosEnvio = 0,
                    CreatedAt = ahora
                };
                await _invitaciones.InsertarAsync(invitacion, innerCt);

                await _eventos.AddAsync(
                    NotificadorRevisionSolicitudes.Evento(
                        AccionReenviada,
                        solicitud,
                        administradorId,
                        new Dictionary<string, string>
                        {
                            ["invitacionAnteriorId"] = anterior.Id.ToString(),
                            ["invitacionId"] = invitacion.Id.ToString()
                        }),
                    innerCt);

                return invitacion;
            },
            ct);

        var emailEnviado = await _notificador.EnviarAsync(
            PlantillasEmail.NombreInvitacionReenviada,
            solicitud,
            administradorId,
            nueva.Id,
            () => _email.EnviarInvitacionReenviadaAsync(solicitud, token.Token, token.ExpiraAt, ct),
            ct);

        return new ReenviarInvitacionResponse(
            nueva.Id,
            emailEnviado,
            emailEnviado ? null : AdvertenciaEmail);
    }
}
