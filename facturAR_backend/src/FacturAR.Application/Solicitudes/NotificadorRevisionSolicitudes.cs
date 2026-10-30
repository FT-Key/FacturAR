using System.Text.Json;
using FacturAR.Application.Abstractions;
using FacturAR.Application.Notificaciones;
using FacturAR.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace FacturAR.Application.Solicitudes;

public sealed class NotificadorRevisionSolicitudes
{
    public const string Entidad = "SolicitudAltaEmpresa";
    public const string ActorAdministrador = "Administrador";

    private const string AccionEmailEnviado = "email.enviado";
    private const string AccionEmailFallido = "email.fallido";

    private readonly IEventoAuditoriaRepository _eventos;
    private readonly IInvitacionRepository _invitaciones;
    private readonly ILogger<NotificadorRevisionSolicitudes> _logger;

    public NotificadorRevisionSolicitudes(
        IEventoAuditoriaRepository eventos,
        IInvitacionRepository invitaciones,
        ILogger<NotificadorRevisionSolicitudes> logger)
    {
        _eventos = eventos;
        _invitaciones = invitaciones;
        _logger = logger;
    }

    public static EventoAuditoria Evento(
        string accion,
        SolicitudAltaEmpresa solicitud,
        Guid administradorId,
        IReadOnlyDictionary<string, string>? datos = null)
    {
        var contenido = new Dictionary<string, string> { ["cuit"] = solicitud.Cuit };
        if (datos is not null)
        {
            foreach (var (clave, valor) in datos)
            {
                contenido[clave] = valor;
            }
        }

        return new EventoAuditoria
        {
            Accion = accion,
            Entidad = Entidad,
            EntidadId = solicitud.Id.ToString(),
            ActorTipo = ActorAdministrador,
            AdministradorId = administradorId,
            ContribuyenteId = null,
            Datos = JsonSerializer.Serialize(contenido)
        };
    }

    public async Task<bool> EnviarAsync(
        string plantilla,
        SolicitudAltaEmpresa solicitud,
        Guid administradorId,
        Guid? invitacionId,
        Func<Task<EmailResult>> enviar,
        CancellationToken ct)
    {
        EmailResult resultado;
        try
        {
            resultado = await enviar();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Falló el envío de email con la plantilla {Plantilla} para la solicitud {SolicitudId}.",
                plantilla,
                solicitud.Id);
            resultado = EmailResult.Fallido("Error de envío.");
        }

        if (!resultado.Exitoso)
        {
            _logger.LogWarning(
                "Falló el envío de email con la plantilla {Plantilla} para la solicitud {SolicitudId}.",
                plantilla,
                solicitud.Id);
        }

        if (invitacionId is Guid id)
        {
            await BestEffortAsync(
                "invitacion.envio",
                solicitud.Id,
                () => _invitaciones.RegistrarEnvioAsync(id, DateTime.UtcNow, ct),
                ct);
        }

        var accion = resultado.Exitoso ? AccionEmailEnviado : AccionEmailFallido;
        await BestEffortAsync(
            accion,
            solicitud.Id,
            () => _eventos.AddAsync(
                Evento(accion, solicitud, administradorId, new Dictionary<string, string> { ["plantilla"] = plantilla }),
                ct),
            ct);

        return resultado.Exitoso;
    }

    public async Task BestEffortAsync(string accion, Guid solicitudId, Func<Task> operacion, CancellationToken ct)
    {
        try
        {
            await operacion();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falló la operación {Accion} para la solicitud {SolicitudId}.", accion, solicitudId);
        }
    }
}
