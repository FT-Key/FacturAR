using System.Collections.Concurrent;
using FacturAR.Application.Abstractions;
using FacturAR.Application.Auditoria;
using FacturAR.Application.Outbox;
using FacturAR.Domain;
using FacturAR.Domain.Entities;
using FacturAR.Infrastructure.Outbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace FacturAR.UnitTests;

public sealed class OutboxWorkerTests
{
    private static readonly TimeSpan Espera = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task ProcesaUnMensajePendienteYLoMarcaProcesado()
    {
        var repositorio = new RepositorioFake();
        var mensaje = repositorio.Agregar("Prueba");
        var handler = new HandlerFake("Prueba");
        var auditoria = new AuditoriaFake();

        await EjecutarAsync(repositorio, [handler], auditoria, Opciones(), async () =>
        {
            await handler.Ejecutado.Task.WaitAsync(Espera);
            await repositorio.Procesado.Task.WaitAsync(Espera);
        });

        Assert.Equal(OutboxEstados.Procesado, mensaje.Estado);
        Assert.Equal(1, mensaje.Intentos);
        Assert.NotNull(mensaje.ProcesadoAt);
        Assert.Null(mensaje.Error);
        Assert.Equal(1, handler.Llamadas);

        var evento = Assert.Single(auditoria.Eventos, e => e.Accion == "OutboxProcesado");
        Assert.Equal(ActorTipos.Sistema, evento.ActorTipo);
        Assert.Equal(EntidadesAuditoria.OutboxMessage, evento.Entidad);
        Assert.Equal(mensaje.Id.ToString(), evento.EntidadId);
        Assert.Null(evento.UsuarioId);
        Assert.Null(evento.AdministradorId);
    }

    [Fact]
    public async Task UnMensajeProcesadoNoSeVuelveAProcesar()
    {
        var repositorio = new RepositorioFake();
        var mensaje = repositorio.Agregar("Prueba");
        var handler = new HandlerFake("Prueba");

        await EjecutarAsync(repositorio, [handler], new AuditoriaFake(), Opciones(), async () =>
        {
            await repositorio.Procesado.Task.WaitAsync(Espera);
            await repositorio.SegundoBarrido.Task.WaitAsync(Espera);
        });

        Assert.Equal(OutboxEstados.Procesado, mensaje.Estado);
        Assert.Equal(1, mensaje.Intentos);
        Assert.Equal(1, handler.Llamadas);
    }

    [Fact]
    public async Task UnHandlerQueFallaRegistraElErrorYVuelveAPendiente()
    {
        var repositorio = new RepositorioFake();
        var mensaje = repositorio.Agregar("Prueba");
        var handler = new HandlerFake(
            "Prueba",
            _ => throw new InvalidOperationException("falló el handler"));
        var auditoria = new AuditoriaFake();

        await EjecutarAsync(repositorio, [handler], auditoria, Opciones(), async () =>
        {
            await repositorio.DevueltoAPendiente.Task.WaitAsync(Espera);
            await Task.Delay(TimeSpan.FromMilliseconds(300));
        });

        Assert.Equal(OutboxEstados.Pendiente, mensaje.Estado);
        Assert.Equal(1, mensaje.Intentos);
        Assert.Equal("Error al procesar el mensaje.", mensaje.Error);
        Assert.Equal(1, handler.Llamadas);
        Assert.DoesNotContain(auditoria.Eventos, e => e.Accion == "OutboxFallido");
    }

    [Fact]
    public async Task AlAlcanzarElMaximoDeIntentosQuedaFallido()
    {
        var repositorio = new RepositorioFake();
        var mensaje = repositorio.Agregar("Prueba");
        mensaje.Intentos = 4;
        var handler = new HandlerFake(
            "Prueba",
            _ => throw new InvalidOperationException("falló el handler"));
        var auditoria = new AuditoriaFake();

        await EjecutarAsync(repositorio, [handler], auditoria, Opciones(), async () =>
            await repositorio.Fallido.Task.WaitAsync(Espera));

        Assert.Equal(OutboxEstados.Fallido, mensaje.Estado);
        Assert.Equal(5, mensaje.Intentos);
        Assert.Equal("Error al procesar el mensaje.", mensaje.Error);

        var evento = Assert.Single(auditoria.Eventos, e => e.Accion == "OutboxFallido");
        Assert.Equal(ActorTipos.Sistema, evento.ActorTipo);
        Assert.Equal(mensaje.Id.ToString(), evento.EntidadId);
    }

    [Fact]
    public async Task RespetaElMaximoDeIntentosConfigurado()
    {
        var repositorio = new RepositorioFake();
        var mensaje = repositorio.Agregar("Prueba");
        mensaje.Intentos = 1;
        var handler = new HandlerFake(
            "Prueba",
            _ => throw new InvalidOperationException("falló el handler"));
        var opciones = Opciones();
        opciones.MaxIntentos = 2;

        await EjecutarAsync(repositorio, [handler], new AuditoriaFake(), opciones, async () =>
            await repositorio.Fallido.Task.WaitAsync(Espera));

        Assert.Equal(OutboxEstados.Fallido, mensaje.Estado);
        Assert.Equal(2, mensaje.Intentos);
    }

    [Fact]
    public async Task UnTipoSinHandlerQuedaFallidoConElErrorCorrespondiente()
    {
        var repositorio = new RepositorioFake();
        var mensaje = repositorio.Agregar("SinHandler");
        var auditoria = new AuditoriaFake();

        await EjecutarAsync(repositorio, [], auditoria, Opciones(), async () =>
            await repositorio.Fallido.Task.WaitAsync(Espera));

        Assert.Equal(OutboxEstados.Fallido, mensaje.Estado);
        Assert.Equal(1, mensaje.Intentos);
        Assert.Equal("No hay handler registrado para el tipo de mensaje.", mensaje.Error);
        Assert.False(repositorio.DevueltoAPendiente.Task.IsCompleted);
        Assert.Single(auditoria.Eventos, e => e.Accion == "OutboxFallido");
    }

    [Fact]
    public async Task SiElRepositorioFallaElWorkerSigueVivo()
    {
        var repositorio = new RepositorioFake
        {
            ErrorReclamo = new InvalidOperationException("Mongo no disponible")
        };

        await EjecutarAsync(repositorio, [], new AuditoriaFake(), Opciones(), async () =>
            await repositorio.SegundoBarrido.Task.WaitAsync(Espera));

        Assert.True(repositorio.Reclamos >= 2);
    }

    [Fact]
    public async Task UnFalloAlMarcarUnMensajeNoAbortaElRestoDelLote()
    {
        var repositorio = new RepositorioFake();
        var primero = repositorio.Agregar("Prueba");
        var segundo = repositorio.Agregar("Prueba");
        repositorio.FallarMarcarProcesado.Add(primero.Id);
        var handler = new HandlerFake("Prueba");
        var opciones = Opciones();
        opciones.IntervaloBarridoSegundos = 60;

        await EjecutarAsync(repositorio, [handler], new AuditoriaFake(), opciones, async () =>
        {
            await repositorio.Procesado.Task.WaitAsync(Espera);
            await repositorio.DevueltoAPendiente.Task.WaitAsync(Espera);
        });

        Assert.Equal(OutboxEstados.Pendiente, primero.Estado);
        Assert.Equal(OutboxEstados.Procesado, segundo.Estado);
        Assert.Equal(2, handler.Llamadas);
    }

    [Fact]
    public async Task ConElWorkerDeshabilitadoNoSeReclamaNada()
    {
        var repositorio = new RepositorioFake();
        repositorio.Agregar("Prueba");
        var handler = new HandlerFake("Prueba");
        var opciones = Opciones();
        opciones.Habilitado = false;

        await EjecutarAsync(repositorio, [handler], new AuditoriaFake(), opciones, async () =>
            await Task.Delay(TimeSpan.FromMilliseconds(300)));

        Assert.Equal(0, repositorio.Reclamos);
        Assert.Equal(0, handler.Llamadas);
    }

    private static OutboxOptions Opciones()
        => new()
        {
            Habilitado = true,
            MaxIntentos = 5,
            IntervaloBaseSegundos = 5,
            IntervaloBarridoSegundos = 1,
            LoteTamano = 20
        };

    private static async Task EjecutarAsync(
        RepositorioFake repositorio,
        IOutboxHandler[] handlers,
        AuditoriaFake auditoria,
        OutboxOptions opciones,
        Func<Task> espera)
    {
        var servicios = new ServiceCollection();
        servicios.AddSingleton<IOutboxRepository>(repositorio);
        servicios.AddSingleton<IAuditoriaService>(auditoria);
        servicios.AddSingleton<OutboxHandlerResolver>();

        foreach (var handler in handlers)
        {
            servicios.AddSingleton(handler);
        }

        using var provider = servicios.BuildServiceProvider();
        var worker = new OutboxWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            opciones,
            new OutboxBackoff(() => 0.5),
            NullLogger<OutboxWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);
        try
        {
            await espera();
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }
    }

    private sealed class HandlerFake : IOutboxHandler
    {
        private readonly Action<OutboxMessage>? _accion;

        public HandlerFake(string tipo, Action<OutboxMessage>? accion = null)
        {
            Tipo = tipo;
            _accion = accion;
        }

        public string Tipo { get; }

        public int Llamadas { get; private set; }

        public TaskCompletionSource Ejecutado { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task HandleAsync(OutboxMessage mensaje, CancellationToken ct)
        {
            Llamadas++;
            Ejecutado.TrySetResult();
            _accion?.Invoke(mensaje);
            return Task.CompletedTask;
        }
    }

    private sealed class AuditoriaFake : IAuditoriaService
    {
        public ConcurrentQueue<EventoFake> Eventos { get; } = new();

        public Task RegistrarAsync(
            ContextoAuditoria contexto,
            string accion,
            string entidad,
            string? entidadId = null,
            string? datos = null,
            CancellationToken ct = default)
        {
            Eventos.Enqueue(new EventoFake(
                accion,
                entidad,
                entidadId,
                contexto.ActorTipo,
                contexto.ContribuyenteId,
                contexto.UsuarioId,
                contexto.AdministradorId));

            return Task.CompletedTask;
        }
    }

    private sealed record EventoFake(
        string Accion,
        string Entidad,
        string? EntidadId,
        string ActorTipo,
        Guid? ContribuyenteId,
        Guid? UsuarioId,
        Guid? AdministradorId);

    private sealed class RepositorioFake : IOutboxRepository
    {
        private readonly object _candado = new();

        public List<OutboxMessage> Mensajes { get; } = [];

        public int Reclamos { get; private set; }

        public Exception? ErrorReclamo { get; set; }

        public TaskCompletionSource Procesado { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Fallido { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource DevueltoAPendiente { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource SegundoBarrido { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public HashSet<Guid> FallarMarcarProcesado { get; } = [];

        public OutboxMessage Agregar(string tipo)
        {
            var mensaje = new OutboxMessage
            {
                Tipo = tipo,
                Payload = "{}",
                Estado = OutboxEstados.Pendiente,
                CreatedAt = DateTime.UtcNow
            };

            lock (_candado)
            {
                Mensajes.Add(mensaje);
            }

            return mensaje;
        }

        public Task<OutboxMessage> EncolarAsync(string tipo, string payload, CancellationToken ct = default)
        {
            var mensaje = new OutboxMessage { Tipo = tipo, Payload = payload };
            lock (_candado)
            {
                Mensajes.Add(mensaje);
            }

            return Task.FromResult(mensaje);
        }

        public Task<IReadOnlyList<OutboxMessage>> ReclamarPendientesAsync(
            int lote,
            IReadOnlyCollection<Guid>? excluir = null,
            CancellationToken ct = default)
        {
            lock (_candado)
            {
                Reclamos++;

                if (Reclamos >= 2)
                {
                    SegundoBarrido.TrySetResult();
                }

                if (ErrorReclamo is not null)
                {
                    throw ErrorReclamo;
                }

                var tomados = Mensajes
                    .Where(m => m.Estado == OutboxEstados.Pendiente
                        && (excluir is null || !excluir.Contains(m.Id)))
                    .OrderBy(m => m.CreatedAt)
                    .Take(lote)
                    .ToList();

                foreach (var mensaje in tomados)
                {
                    mensaje.Estado = OutboxEstados.Procesando;
                    mensaje.Intentos++;
                }

                return Task.FromResult<IReadOnlyList<OutboxMessage>>(tomados);
            }
        }

        public Task MarcarProcesadoAsync(Guid id, CancellationToken ct = default)
        {
            lock (_candado)
            {
                if (FallarMarcarProcesado.Contains(id))
                {
                    throw new InvalidOperationException("no se pudo marcar el mensaje como procesado");
                }

                var mensaje = Mensajes.Single(m => m.Id == id);
                mensaje.Estado = OutboxEstados.Procesado;
                mensaje.ProcesadoAt = DateTime.UtcNow;
                mensaje.Error = null;
            }

            Procesado.TrySetResult();
            return Task.CompletedTask;
        }

        public Task MarcarFallidoAsync(Guid id, string error, CancellationToken ct = default)
        {
            lock (_candado)
            {
                var mensaje = Mensajes.Single(m => m.Id == id);
                mensaje.Estado = OutboxEstados.Fallido;
                mensaje.Error = error;
            }

            Fallido.TrySetResult();
            return Task.CompletedTask;
        }

        public Task DevolverAPendienteAsync(Guid id, string? error, CancellationToken ct = default)
        {
            lock (_candado)
            {
                var mensaje = Mensajes.Single(m => m.Id == id);
                mensaje.Estado = OutboxEstados.Pendiente;
                mensaje.Error = error;
            }

            DevueltoAPendiente.TrySetResult();
            return Task.CompletedTask;
        }

        public Task<OutboxMessage?> ConsultarAsync(Guid id, CancellationToken ct = default)
        {
            lock (_candado)
            {
                return Task.FromResult(Mensajes.SingleOrDefault(m => m.Id == id));
            }
        }

        public Task<IReadOnlyList<OutboxMessage>> ListarPorEstadoAsync(
            string estado,
            DateTime? desde,
            DateTime? hasta,
            int limite,
            CancellationToken ct = default)
        {
            lock (_candado)
            {
                return Task.FromResult<IReadOnlyList<OutboxMessage>>(
                    Mensajes
                        .Where(m => m.Estado == estado
                            && (desde is null || m.CreatedAt >= desde)
                            && (hasta is null || m.CreatedAt <= hasta))
                        .Take(limite)
                        .ToList());
            }
        }
    }
}
