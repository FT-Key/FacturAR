using System.Text.Json;
using FacturAR.Application.Abstractions;
using FacturAR.Application.Notificaciones;
using FacturAR.Contracts.Solicitudes;
using FacturAR.Domain.Entities;
using FacturAR.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace FacturAR.Application.Solicitudes;

public sealed class CrearSolicitudAltaService
{
    public const string CodigoSolicitudRecibida = "SOLICITUD_RECIBIDA";
    public const string MensajeSolicitudRecibida = "Recibimos tu solicitud. Revisamos los datos y te escribimos al email de contacto.";
    public const string ActorSistema = "Sistema";

    private const string AccionSolicitudCreada = "solicitud.creada";
    private const string AccionCuitRegistrado = "solicitud.cuit_registrado";
    private const string AccionEmailEnviado = "email.enviado";
    private const string AccionEmailFallido = "email.fallido";
    private const string Entidad = "SolicitudAltaEmpresa";

    private readonly ISolicitudAltaRepository _solicitudes;
    private readonly IContribuyenteRepository _contribuyentes;
    private readonly IEventoAuditoriaRepository _eventos;
    private readonly EmailService _email;
    private readonly ILogger<CrearSolicitudAltaService> _logger;

    public CrearSolicitudAltaService(
        ISolicitudAltaRepository solicitudes,
        IContribuyenteRepository contribuyentes,
        IEventoAuditoriaRepository eventos,
        EmailService email,
        ILogger<CrearSolicitudAltaService> logger)
    {
        _solicitudes = solicitudes;
        _contribuyentes = contribuyentes;
        _eventos = eventos;
        _email = email;
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

        var recibida = await EnviarAsync(
            PlantillasEmail.NombreSolicitudRecibida,
            solicitud.Id,
            () => _email.EnviarSolicitudRecibidaAsync(solicitud, ct),
            ct);
        await AuditarEmailAsync(solicitud, PlantillasEmail.NombreSolicitudRecibida, recibida, ct);

        if (_email.NotificacionAdministracionConfigurada)
        {
            var nueva = await EnviarAsync(
                PlantillasEmail.NombreNuevaSolicitud,
                solicitud.Id,
                () => _email.EnviarNuevaSolicitudAsync(solicitud, ct),
                ct);
            await AuditarEmailAsync(solicitud, PlantillasEmail.NombreNuevaSolicitud, nueva, ct);
        }

        return Respuesta();
    }

    private static SolicitudAltaResponse Respuesta()
        => new(CodigoSolicitudRecibida, MensajeSolicitudRecibida);

    private async Task AuditarEmailAsync(
        SolicitudAltaEmpresa solicitud,
        string plantilla,
        EmailResult resultado,
        CancellationToken ct)
    {
        var accion = resultado.Exitoso ? AccionEmailEnviado : AccionEmailFallido;

        await BestEffortAsync(
            accion,
            solicitud.Id,
            () => _eventos.AddAsync(
                new EventoAuditoria
                {
                    Accion = accion,
                    Entidad = Entidad,
                    EntidadId = solicitud.Id.ToString(),
                    ActorTipo = ActorSistema,
                    Datos = Datos(solicitud.Cuit, plantilla)
                },
                ct),
            ct);
    }

    private Task AuditarSolicitudCreadaAsync(SolicitudAltaEmpresa solicitud, CancellationToken ct)
        => _eventos.AddAsync(
            new EventoAuditoria
            {
                Accion = AccionSolicitudCreada,
                Entidad = Entidad,
                EntidadId = solicitud.Id.ToString(),
                ActorTipo = ActorSistema,
                Datos = Datos(solicitud.Cuit, null)
            },
            ct);

    private Task AuditarCuitRegistradoAsync(SolicitudAltaEmpresa solicitud, CancellationToken ct)
        => _eventos.AddAsync(
            new EventoAuditoria
            {
                Accion = AccionCuitRegistrado,
                Entidad = Entidad,
                ActorTipo = ActorSistema,
                Datos = Datos(solicitud.Cuit, null)
            },
            ct);

    private static string Datos(string cuit, string? plantilla)
    {
        var datos = new Dictionary<string, string> { ["cuit"] = cuit };
        if (plantilla is not null)
        {
            datos["plantilla"] = plantilla;
        }

        return JsonSerializer.Serialize(datos);
    }

    private async Task<EmailResult> EnviarAsync(
        string plantilla,
        Guid solicitudId,
        Func<Task<EmailResult>> enviar,
        CancellationToken ct)
    {
        try
        {
            var resultado = await enviar();
            if (!resultado.Exitoso)
            {
                _logger.LogWarning(
                    "Falló el envío de email {Accion} con la plantilla {Plantilla} para la solicitud {SolicitudId}.",
                    AccionEmailFallido,
                    plantilla,
                    solicitudId);
            }

            return resultado;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Falló el envío de email {Accion} con la plantilla {Plantilla} para la solicitud {SolicitudId}.",
                AccionEmailFallido,
                plantilla,
                solicitudId);
            return EmailResult.Fallido("Error de envío.");
        }
    }

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
