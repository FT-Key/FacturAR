using System.Text.Json;
using FacturAR.Application.Abstractions;
using FacturAR.Application.Auditoria;
using FacturAR.Application.Notificaciones;
using FacturAR.Application.Outbox;
using FacturAR.Contracts.Solicitudes;
using FacturAR.Domain;
using FacturAR.Domain.Entities;
using FacturAR.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace FacturAR.Application.Solicitudes;

public sealed class CrearSolicitudAltaService
{
    public const string CodigoSolicitudRecibida = "SOLICITUD_RECIBIDA";
    public const string MensajeSolicitudRecibida = "Recibimos tu solicitud. Revisamos los datos y te escribimos al email de contacto.";
    public const string ActorSistema = ActorTipos.Sistema;

    private const string AccionSolicitudCreada = AccionesAuditoria.SolicitudCreada;
    private const string AccionCuitRegistrado = AccionesAuditoria.SolicitudCuitRegistrado;
    private const string Entidad = EntidadesAuditoria.SolicitudAltaEmpresa;

    private readonly ISolicitudAltaRepository _solicitudes;
    private readonly IContribuyenteRepository _contribuyentes;
    private readonly IEventoAuditoriaRepository _eventos;
    private readonly IOutboxRepository _outbox;
    private readonly NotificacionesOptions _notificaciones;
    private readonly ILogger<CrearSolicitudAltaService> _logger;

    public CrearSolicitudAltaService(
        ISolicitudAltaRepository solicitudes,
        IContribuyenteRepository contribuyentes,
        IEventoAuditoriaRepository eventos,
        IOutboxRepository outbox,
        NotificacionesOptions notificaciones,
        ILogger<CrearSolicitudAltaService> logger)
    {
        _solicitudes = solicitudes;
        _contribuyentes = contribuyentes;
        _eventos = eventos;
        _outbox = outbox;
        _notificaciones = notificaciones;
        _logger = logger;
    }

    public async Task<SolicitudAltaResponse> CrearAsync(
        SolicitudAltaRequest request,
        CancellationToken ct = default)
    {
        var solicitud = SolicitudAltaValidator.Validar(request);

        if (await _solicitudes.ExistePendientePorEmailAsync(solicitud.EmailSolicitante, ct))
        {
            throw new SolicitudPendienteException();
        }

        if (await _solicitudes.ExistePendientePorCuitAsync(solicitud.Cuit, ct))
        {
            throw new SolicitudPendienteException();
        }

        if (await _contribuyentes.ExistePorCuitAsync(solicitud.Cuit, ct))
        {
            await BestEffortAsync(
                AccionCuitRegistrado,
                solicitud.Id,
                () => AuditarCuitRegistradoAsync(solicitud, ct),
                ct);
            return Respuesta();
        }

        await _solicitudes.InsertarAsync(solicitud, ct);

        await BestEffortAsync(
            AccionSolicitudCreada,
            solicitud.Id,
            () => AuditarSolicitudCreadaAsync(solicitud, ct),
            ct);

        await EncolarAsync(TiposMensajeOutbox.EmailSolicitudRecibida, solicitud.Id, ct);

        if (NotificacionAdministracionConfigurada)
        {
            await EncolarAsync(TiposMensajeOutbox.EmailNuevaSolicitud, solicitud.Id, ct);
        }

        return Respuesta();
    }

    private bool NotificacionAdministracionConfigurada
        => !string.IsNullOrWhiteSpace(_notificaciones.AdminNotificationEmail);

    private static SolicitudAltaResponse Respuesta()
        => new(CodigoSolicitudRecibida, MensajeSolicitudRecibida);

    private async Task EncolarAsync(string tipo, Guid solicitudId, CancellationToken ct)
    {
        try
        {
            await _outbox.EncolarAsync(tipo, PayloadEmailSolicitud.Crear(solicitudId), ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Falló el encolado del mensaje {Tipo} para la solicitud {SolicitudId}.",
                tipo,
                solicitudId);
        }
    }

    private Task AuditarSolicitudCreadaAsync(SolicitudAltaEmpresa solicitud, CancellationToken ct)
        => _eventos.AddAsync(
            new EventoAuditoria
            {
                Accion = AccionSolicitudCreada,
                Entidad = Entidad,
                EntidadId = solicitud.Id.ToString(),
                ActorTipo = ActorSistema,
                Datos = Datos(solicitud.Cuit)
            },
            ct);

    private Task AuditarCuitRegistradoAsync(SolicitudAltaEmpresa solicitud, CancellationToken ct)
        => _eventos.AddAsync(
            new EventoAuditoria
            {
                Accion = AccionCuitRegistrado,
                Entidad = Entidad,
                ActorTipo = ActorSistema,
                Datos = Datos(solicitud.Cuit)
            },
            ct);

    private static string Datos(string cuit)
        => JsonSerializer.Serialize(new Dictionary<string, string> { ["cuit"] = cuit });

    private async Task BestEffortAsync(
        string accion,
        Guid solicitudId,
        Func<Task> operacion,
        CancellationToken ct)
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
            _logger.LogWarning(
                ex,
                "Falló la auditoría {Accion} para la solicitud {SolicitudId}.",
                accion,
                solicitudId);
        }
    }
}
