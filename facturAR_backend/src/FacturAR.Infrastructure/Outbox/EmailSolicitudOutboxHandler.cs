using System.Text.Json;
using FacturAR.Application.Abstractions;
using FacturAR.Application.Auditoria;
using FacturAR.Application.Notificaciones;
using FacturAR.Application.Outbox;
using FacturAR.Domain;
using FacturAR.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace FacturAR.Infrastructure.Outbox;

public abstract class EmailSolicitudOutboxHandler : IOutboxHandler
{
    private const string Entidad = EntidadesAuditoria.SolicitudAltaEmpresa;

    private readonly string _plantilla;
    private readonly Func<SolicitudAltaEmpresa, CancellationToken, Task<EmailResult>> _enviar;
    private readonly ISolicitudAltaRepository _solicitudes;
    private readonly IEventoAuditoriaRepository _eventos;
    private readonly ILogger _logger;

    protected EmailSolicitudOutboxHandler(
        string tipo,
        string plantilla,
        Func<SolicitudAltaEmpresa, CancellationToken, Task<EmailResult>> enviar,
        ISolicitudAltaRepository solicitudes,
        IEventoAuditoriaRepository eventos,
        ILogger logger)
    {
        Tipo = tipo;
        _plantilla = plantilla;
        _enviar = enviar;
        _solicitudes = solicitudes;
        _eventos = eventos;
        _logger = logger;
    }

    public string Tipo { get; }

    public async Task HandleAsync(OutboxMessage mensaje, CancellationToken ct)
    {
        var solicitudId = PayloadEmailSolicitud.Leer(mensaje.Payload);
        if (solicitudId is null)
        {
            _logger.LogWarning(
                "El mensaje de outbox {MensajeId} de tipo {Tipo} no tiene un payload válido.",
                mensaje.Id,
                Tipo);
            return;
        }

        var solicitud = await _solicitudes.ObtenerPorIdAsync(solicitudId.Value, ct);
        if (solicitud is null)
        {
            _logger.LogWarning(
                "La solicitud {SolicitudId} del mensaje de outbox {MensajeId} de tipo {Tipo} ya no existe.",
                solicitudId,
                mensaje.Id,
                Tipo);
            return;
        }

        var resultado = await EnviarAsync(solicitud, ct);
        await AuditarAsync(solicitud, resultado, ct);

        if (!resultado.Exitoso)
        {
            throw new InvalidOperationException(
                $"No se pudo enviar el email de la plantilla {_plantilla} para la solicitud {solicitud.Id}.");
        }
    }

    private async Task<EmailResult> EnviarAsync(SolicitudAltaEmpresa solicitud, CancellationToken ct)
    {
        try
        {
            return await _enviar(solicitud, ct);
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
                _plantilla,
                solicitud.Id);
            return EmailResult.Fallido("Error de envío.");
        }
    }

    private async Task AuditarAsync(
        SolicitudAltaEmpresa solicitud,
        EmailResult resultado,
        CancellationToken ct)
    {
        var accion = resultado.Exitoso
            ? AccionesAuditoria.EmailEnviado
            : AccionesAuditoria.EmailFallido;

        try
        {
            await _eventos.AddAsync(
                new EventoAuditoria
                {
                    Accion = accion,
                    Entidad = Entidad,
                    EntidadId = solicitud.Id.ToString(),
                    ActorTipo = ActorTipos.Sistema,
                    Datos = Datos(solicitud.Cuit)
                },
                ct);
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
                solicitud.Id);
        }
    }

    private string Datos(string cuit)
        => JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["cuit"] = cuit,
            ["plantilla"] = _plantilla
        });
}
