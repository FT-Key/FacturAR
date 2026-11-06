using FacturAR.Application.Abstractions;
using FacturAR.Application.Auditoria;
using FacturAR.Application.Outbox;
using FacturAR.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FacturAR.Infrastructure.Outbox;

public sealed class OutboxWorker : BackgroundService
{
    private const string ErrorSinHandler = "No hay handler registrado para el tipo de mensaje.";
    private const string ErrorGenerico = "Error al procesar el mensaje.";
    private const int MaximoLargoError = 500;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly OutboxOptions _options;
    private readonly OutboxBackoff _backoff;
    private readonly ILogger<OutboxWorker> _logger;
    private readonly Dictionary<Guid, DateTime> _proximoIntento = [];

    public OutboxWorker(
        IServiceScopeFactory scopeFactory,
        OutboxOptions options,
        OutboxBackoff backoff,
        ILogger<OutboxWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _backoff = backoff;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Habilitado)
        {
            _logger.LogInformation("El worker de outbox está deshabilitado.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await EjecutarCicloAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            try
            {
                await Task.Delay(IntervaloBarrido, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private TimeSpan IntervaloBarrido
        => TimeSpan.FromSeconds(Math.Max(1, _options.IntervaloBarridoSegundos));

    private async Task EjecutarCicloAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var outbox = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
            var auditoria = scope.ServiceProvider.GetService<IAuditoriaService>();
            var resolver = scope.ServiceProvider.GetRequiredService<OutboxHandlerResolver>();

            var mensajes = await outbox.ReclamarPendientesAsync(
                Math.Max(1, _options.LoteTamano),
                EnBackoff(),
                ct);

            foreach (var mensaje in mensajes)
            {
                try
                {
                    await ProcesarMensajeAsync(outbox, auditoria, resolver, mensaje, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        "Falló el procesamiento del mensaje de outbox {MensajeId} de tipo {Tipo}. Excepción: {Excepcion}.",
                        mensaje.Id,
                        mensaje.Tipo,
                        ex.GetType().Name);

                    await CompensarAsync(outbox, mensaje, ct);
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falló el ciclo del worker de outbox.");
        }
    }

    private async Task ProcesarMensajeAsync(
        IOutboxRepository outbox,
        IAuditoriaService? auditoria,
        OutboxHandlerResolver resolver,
        OutboxMessage mensaje,
        CancellationToken ct)
    {
        var handler = resolver.Obtener(mensaje.Tipo);
        if (handler is null)
        {
            await FallarDefinitivoAsync(outbox, auditoria, mensaje, ErrorSinHandler, ct);
            return;
        }

        try
        {
            await handler.HandleAsync(mensaje, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                "Falló el handler del mensaje de outbox {MensajeId} de tipo {Tipo}. Excepción: {Excepcion}.",
                mensaje.Id,
                mensaje.Tipo,
                ex.GetType().Name);

            await FallarAsync(outbox, auditoria, mensaje, ErrorGenerico, ct);
            return;
        }

        await outbox.MarcarProcesadoAsync(mensaje.Id, ct);
        _proximoIntento.Remove(mensaje.Id);
        await AuditarAsync(auditoria, AccionesAuditoria.OutboxProcesado, mensaje, ct);
    }

    private async Task FallarAsync(
        IOutboxRepository outbox,
        IAuditoriaService? auditoria,
        OutboxMessage mensaje,
        string error,
        CancellationToken ct)
    {
        var maximoIntentos = Math.Max(1, _options.MaxIntentos);

        if (mensaje.Intentos >= maximoIntentos)
        {
            await FallarDefinitivoAsync(outbox, auditoria, mensaje, error, ct);
            return;
        }

        await outbox.DevolverAPendienteAsync(mensaje.Id, Recortar(error), ct);
        _proximoIntento[mensaje.Id] = DateTime.UtcNow
            + _backoff.Calcular(mensaje.Intentos, _options.IntervaloBaseSegundos);
    }

    private async Task FallarDefinitivoAsync(
        IOutboxRepository outbox,
        IAuditoriaService? auditoria,
        OutboxMessage mensaje,
        string error,
        CancellationToken ct)
    {
        await outbox.MarcarFallidoAsync(mensaje.Id, Recortar(error), ct);
        _proximoIntento.Remove(mensaje.Id);
        await AuditarAsync(auditoria, AccionesAuditoria.OutboxFallido, mensaje, ct);
    }

    private async Task CompensarAsync(IOutboxRepository outbox, OutboxMessage mensaje, CancellationToken ct)
    {
        try
        {
            await outbox.DevolverAPendienteAsync(mensaje.Id, null, ct);
            _proximoIntento.Remove(mensaje.Id);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                "No se pudo devolver a pendiente el mensaje de outbox {MensajeId} de tipo {Tipo}. Excepción: {Excepcion}.",
                mensaje.Id,
                mensaje.Tipo,
                ex.GetType().Name);
        }
    }

    private async Task AuditarAsync(
        IAuditoriaService? auditoria,
        string accion,
        OutboxMessage mensaje,
        CancellationToken ct)
    {
        if (auditoria is null)
        {
            return;
        }

        try
        {
            await auditoria.RegistrarAsync(
                ContextoAuditoria.Sistema(),
                accion,
                EntidadesAuditoria.OutboxMessage,
                mensaje.Id.ToString(),
                datos: null,
                ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            _logger.LogWarning("No se pudo auditar el mensaje de outbox {MensajeId}.", mensaje.Id);
        }
    }

    private IReadOnlyCollection<Guid>? EnBackoff()
    {
        if (_proximoIntento.Count == 0)
        {
            return null;
        }

        var ahora = DateTime.UtcNow;
        var enEspera = new List<Guid>();
        var vencidos = new List<Guid>();

        foreach (var (id, programado) in _proximoIntento)
        {
            if (programado > ahora)
            {
                enEspera.Add(id);
            }
            else
            {
                vencidos.Add(id);
            }
        }

        foreach (var id in vencidos)
        {
            _proximoIntento.Remove(id);
        }

        return enEspera.Count == 0 ? null : enEspera;
    }

    private static string Recortar(string? mensaje)
    {
        var texto = string.IsNullOrWhiteSpace(mensaje) ? ErrorGenerico : mensaje.Trim();
        return texto.Length <= MaximoLargoError ? texto : texto[..MaximoLargoError];
    }
}
