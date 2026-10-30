using FacturAR.Application.Abstractions;
using FacturAR.Application.Notificaciones;
using FacturAR.Contracts.Solicitudes;
using FacturAR.Domain.Enums;
using FacturAR.Domain.Exceptions;

namespace FacturAR.Application.Solicitudes;

public sealed class RechazarSolicitudService
{
    public const string AdvertenciaEmail = "No se pudo enviar el email de rechazo al solicitante.";
    public const string AccionRechazada = "solicitud.rechazada";
    public const string MensajeMotivoObligatorio = "El motivo del rechazo es obligatorio.";

    private const int MotivoMinimo = 10;
    private const int MotivoMaximo = 500;

    private readonly ISolicitudAltaRepository _solicitudes;
    private readonly IEventoAuditoriaRepository _eventos;
    private readonly ITransaccionMongo _transaccion;
    private readonly EmailService _email;
    private readonly NotificadorRevisionSolicitudes _notificador;

    public RechazarSolicitudService(
        ISolicitudAltaRepository solicitudes,
        IEventoAuditoriaRepository eventos,
        ITransaccionMongo transaccion,
        EmailService email,
        NotificadorRevisionSolicitudes notificador)
    {
        _solicitudes = solicitudes;
        _eventos = eventos;
        _transaccion = transaccion;
        _email = email;
        _notificador = notificador;
    }

    public async Task<RechazarSolicitudResponse> RechazarAsync(
        Guid solicitudId,
        string? motivo,
        Guid administradorId,
        CancellationToken ct = default)
    {
        var motivoNormalizado = (motivo ?? string.Empty).Trim();
        if (motivoNormalizado.Length is < MotivoMinimo or > MotivoMaximo)
        {
            throw new SolicitudAltaValidationException(MensajeMotivoObligatorio);
        }

        var solicitud = await _solicitudes.ObtenerPorIdAsync(solicitudId, ct)
            ?? throw new SolicitudNoEncontradaException();

        if (solicitud.Estado != EstadoSolicitud.Pendiente)
        {
            throw new SolicitudYaProcesadaException();
        }

        var ahora = DateTime.UtcNow;

        await _transaccion.EjecutarAsync(
            async innerCt =>
            {
                if (!await _solicitudes.RechazarPendienteAsync(
                        solicitudId,
                        motivoNormalizado,
                        administradorId,
                        ahora,
                        innerCt))
                {
                    throw new SolicitudYaProcesadaException();
                }

                await _eventos.AddAsync(
                    NotificadorRevisionSolicitudes.Evento(
                        AccionRechazada,
                        solicitud,
                        administradorId,
                        new Dictionary<string, string> { ["motivo"] = motivoNormalizado }),
                    innerCt);

                return true;
            },
            ct);

        solicitud.Estado = EstadoSolicitud.Rechazada;
        solicitud.MotivoRechazo = motivoNormalizado;
        solicitud.RevisadaPorAdministradorId = administradorId;
        solicitud.RevisadaAt = ahora;
        solicitud.UpdatedAt = ahora;

        var emailEnviado = await _notificador.EnviarAsync(
            PlantillasEmail.NombreSolicitudRechazada,
            solicitud,
            administradorId,
            null,
            () => _email.EnviarSolicitudRechazadaAsync(solicitud, ct),
            ct);

        return new RechazarSolicitudResponse(
            nameof(EstadoSolicitud.Rechazada),
            emailEnviado,
            emailEnviado ? null : AdvertenciaEmail);
    }
}
