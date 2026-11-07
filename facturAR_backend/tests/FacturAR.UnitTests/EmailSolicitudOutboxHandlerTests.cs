using FacturAR.Application.Abstractions;
using FacturAR.Application.Auditoria;
using FacturAR.Application.Notificaciones;
using FacturAR.Application.Outbox;
using FacturAR.Domain;
using FacturAR.Domain.Entities;
using FacturAR.Infrastructure.Outbox;
using Microsoft.Extensions.Logging.Abstractions;

namespace FacturAR.UnitTests;

public sealed class EmailSolicitudOutboxHandlerTests
{
    private const string CuitValido = "20123456786";
    private const string EmailSolicitante = "solicitante@demo.com";

    [Fact]
    public void Tipo_CoincideConElMensajeEncolado()
    {
        var contexto = new Contexto();

        Assert.Equal(TiposMensajeOutbox.EmailSolicitudRecibida, contexto.Recibida.Tipo);
        Assert.Equal(TiposMensajeOutbox.EmailNuevaSolicitud, contexto.Nueva.Tipo);
    }

    [Fact]
    public async Task HandleAsync_ConSolicitudExistente_EnviaYAuditaElEmailEnviado()
    {
        var contexto = new Contexto();
        var solicitud = contexto.Solicitud();

        await contexto.Recibida.HandleAsync(Mensaje(contexto.Recibida.Tipo, solicitud.Id), default);

        var email = Assert.Single(contexto.Emails.Enviados);
        Assert.Equal(EmailSolicitante, email.Para);
        Assert.Equal(PlantillasEmail.NombreSolicitudRecibida, email.Plantilla);

        var evento = Assert.Single(contexto.Eventos.Eventos);
        Assert.Equal(AccionesAuditoria.EmailEnviado, evento.Accion);
        Assert.Equal(EntidadesAuditoria.SolicitudAltaEmpresa, evento.Entidad);
        Assert.Equal(solicitud.Id.ToString(), evento.EntidadId);
        Assert.Equal(ActorTipos.Sistema, evento.ActorTipo);
        Assert.Contains(CuitValido, evento.Datos);
        Assert.DoesNotContain(EmailSolicitante, evento.Datos);
    }

    [Fact]
    public async Task HandleAsync_ConEnvioFallido_AuditaElFalloYLanzaParaReintentar()
    {
        var contexto = new Contexto();
        var solicitud = contexto.Solicitud();
        contexto.Emails.Excepcion = new InvalidOperationException("SMTP caído.");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => contexto.Recibida.HandleAsync(Mensaje(contexto.Recibida.Tipo, solicitud.Id), default));

        Assert.Empty(contexto.Emails.Enviados);
        var evento = Assert.Single(contexto.Eventos.Eventos);
        Assert.Equal(AccionesAuditoria.EmailFallido, evento.Accion);
        Assert.DoesNotContain(EmailSolicitante, evento.Datos);
    }

    [Fact]
    public async Task HandleAsync_ConSolicitudInexistente_TerminaSinEnviarNiAuditar()
    {
        var contexto = new Contexto();

        await contexto.Recibida.HandleAsync(Mensaje(contexto.Recibida.Tipo, Guid.NewGuid()), default);

        Assert.Empty(contexto.Emails.Enviados);
        Assert.Empty(contexto.Eventos.Eventos);
    }

    [Fact]
    public async Task HandleAsync_ConPayloadInvalido_TerminaSinEnviarNiAuditar()
    {
        var contexto = new Contexto();
        var mensaje = new OutboxMessage { Tipo = contexto.Recibida.Tipo, Payload = """{"otro":"valor"}""" };

        await contexto.Recibida.HandleAsync(mensaje, default);

        Assert.Empty(contexto.Emails.Enviados);
        Assert.Empty(contexto.Eventos.Eventos);
    }

    [Fact]
    public async Task HandleAsync_ConAdministracionConfigurada_EnviaLaNuevaSolicitudAlAdministrador()
    {
        var contexto = new Contexto("admin@facturar.com");
        var solicitud = contexto.Solicitud();

        await contexto.Nueva.HandleAsync(Mensaje(contexto.Nueva.Tipo, solicitud.Id), default);

        var email = Assert.Single(contexto.Emails.Enviados);
        Assert.Equal("admin@facturar.com", email.Para);
        Assert.Equal(PlantillasEmail.NombreNuevaSolicitud, email.Plantilla);
        Assert.Equal(AccionesAuditoria.EmailEnviado, Assert.Single(contexto.Eventos.Eventos).Accion);
    }

    private static OutboxMessage Mensaje(string tipo, Guid solicitudId)
        => new() { Tipo = tipo, Payload = PayloadEmailSolicitud.Crear(solicitudId) };

    private sealed class Contexto
    {
        public Contexto(string? adminNotificationEmail = null)
        {
            var opciones = new NotificacionesOptions { AdminNotificationEmail = adminNotificationEmail };
            var email = new EmailService(Emails, opciones);

            Recibida = new EmailSolicitudRecibidaOutboxHandler(
                Solicitudes,
                email,
                Eventos,
                NullLogger<EmailSolicitudRecibidaOutboxHandler>.Instance);
            Nueva = new EmailNuevaSolicitudOutboxHandler(
                Solicitudes,
                email,
                Eventos,
                NullLogger<EmailNuevaSolicitudOutboxHandler>.Instance);
        }

        public FakeSolicitudAltaRepository Solicitudes { get; } = new();

        public FakeEventoAuditoriaRepository Eventos { get; } = new();

        public FakeEmailSender Emails { get; } = new();

        public EmailSolicitudRecibidaOutboxHandler Recibida { get; }

        public EmailNuevaSolicitudOutboxHandler Nueva { get; }

        public SolicitudAltaEmpresa Solicitud()
        {
            var solicitud = new SolicitudAltaEmpresa
            {
                RazonSocial = "ACME S.A.",
                Cuit = CuitValido,
                NombreSolicitante = "Ana Pérez",
                EmailSolicitante = EmailSolicitante
            };
            Solicitudes.Existentes[solicitud.Id] = solicitud;
            return solicitud;
        }
    }

    private sealed class FakeSolicitudAltaRepository : ISolicitudAltaRepository
    {
        public Dictionary<Guid, SolicitudAltaEmpresa> Existentes { get; } = [];

        public Task InsertarAsync(SolicitudAltaEmpresa solicitud, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<bool> ExistePendientePorCuitAsync(string cuit, CancellationToken ct = default)
            => Task.FromResult(false);

        public Task<bool> ExistePendientePorEmailAsync(string email, CancellationToken ct = default)
            => Task.FromResult(false);

        public Task<SolicitudAltaEmpresa?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(Existentes.GetValueOrDefault(id));

        public Task<(IReadOnlyList<SolicitudAltaEmpresa> Items, long Total)> ListarAsync(
            FiltroSolicitudes filtro,
            CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<bool> ActualizarDatosPendienteAsync(SolicitudAltaEmpresa solicitud, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<bool> AprobarPendienteAsync(
            Guid id,
            Guid contribuyenteId,
            Guid invitacionId,
            Guid administradorId,
            DateTime revisadaAt,
            CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<bool> RechazarPendienteAsync(
            Guid id,
            string motivo,
            Guid administradorId,
            DateTime revisadaAt,
            CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    private sealed class FakeEventoAuditoriaRepository : IEventoAuditoriaRepository
    {
        public List<EventoAuditoria> Eventos { get; } = [];

        public Task AddAsync(EventoAuditoria evento, CancellationToken ct = default)
        {
            Eventos.Add(evento);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<EventoAuditoria>> ListarPorEntidadAsync(
            string entidad,
            string entidadId,
            CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    private sealed class FakeEmailSender : IEmailSender
    {
        public List<EmailMessage> Enviados { get; } = [];

        public Exception? Excepcion { get; set; }

        public Task<EmailResult> EnviarAsync(
            EmailMessage mensaje,
            CancellationToken cancellationToken = default)
        {
            if (Excepcion is not null)
            {
                throw Excepcion;
            }

            Enviados.Add(mensaje);
            return Task.FromResult(EmailResult.Ok(DateTime.UtcNow));
        }
    }
}
