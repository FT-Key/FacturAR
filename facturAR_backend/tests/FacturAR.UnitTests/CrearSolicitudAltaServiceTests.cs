using FacturAR.Application.Abstractions;
using FacturAR.Application.Auditoria;
using FacturAR.Application.Notificaciones;
using FacturAR.Application.Outbox;
using FacturAR.Application.Solicitudes;
using FacturAR.Contracts.Solicitudes;
using FacturAR.Domain;
using FacturAR.Domain.Entities;
using FacturAR.Domain.Enums;
using FacturAR.Domain.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;

namespace FacturAR.UnitTests;

public sealed class CrearSolicitudAltaServiceTests
{
    private const string CuitValido = "20123456786";
    private const string EmailValido = "solicitante@demo.com";

    [Fact]
    public async Task CrearAsync_ConSolicitudValida_NormalizaYGuardaPendienteEnUtc()
    {
        var contexto = new Contexto();
        var antes = DateTime.UtcNow;

        var respuesta = await contexto.Service.CrearAsync(
            Request() with { Cuit = "20-12345678-6", Email = "  Solicitante@Demo.COM  " });

        var despues = DateTime.UtcNow;
        Assert.Equal("SOLICITUD_RECIBIDA", respuesta.Codigo);
        Assert.Equal(CrearSolicitudAltaService.MensajeSolicitudRecibida, respuesta.Mensaje);

        var solicitud = Assert.Single(contexto.Solicitudes.Insertadas);
        Assert.Equal(CuitValido, solicitud.Cuit);
        Assert.Equal("solicitante@demo.com", solicitud.EmailSolicitante);
        Assert.Equal("ACME S.A.", solicitud.RazonSocial);
        Assert.Equal(EstadoSolicitud.Pendiente, solicitud.Estado);
        Assert.True(solicitud.AceptaTerminos);
        Assert.Null(solicitud.ContribuyenteId);
        Assert.Null(solicitud.MotivoRechazo);

        Assert.Equal(DateTimeKind.Utc, solicitud.CreatedAt.Kind);
        Assert.Equal(DateTimeKind.Utc, solicitud.UpdatedAt.Kind);
        Assert.Equal(DateTimeKind.Utc, solicitud.TerminosAceptadosAt.Kind);
        Assert.Equal(solicitud.CreatedAt, solicitud.UpdatedAt);
        Assert.Equal(solicitud.CreatedAt, solicitud.TerminosAceptadosAt);
        Assert.InRange(solicitud.CreatedAt, antes, despues);
    }

    [Fact]
    public async Task CrearAsync_ConSolicitudValida_EncolaLaConfirmacionSinDatosPersonales()
    {
        var contexto = new Contexto();

        await contexto.Service.CrearAsync(Request());

        var solicitud = Assert.Single(contexto.Solicitudes.Insertadas);
        var mensaje = Assert.Single(contexto.Outbox.Encolados);
        Assert.Equal(TiposMensajeOutbox.EmailSolicitudRecibida, mensaje.Tipo);
        Assert.Equal($"{{\"solicitudId\":\"{solicitud.Id}\"}}", mensaje.Payload);
        Assert.Equal(solicitud.Id, PayloadEmailSolicitud.Leer(mensaje.Payload));
        Assert.DoesNotContain(EmailValido, mensaje.Payload);
        Assert.DoesNotContain("Ana Pérez", mensaje.Payload);
        Assert.Contains(AccionesAuditoria.SolicitudCreada, contexto.Eventos.Acciones);
        Assert.DoesNotContain(AccionesAuditoria.EmailEnviado, contexto.Eventos.Acciones);
    }

    [Theory]
    [InlineData("razon-social-vacia", "La razón social es obligatoria.")]
    [InlineData("razon-social-corta", "La razón social debe tener entre 3 y 200 caracteres.")]
    [InlineData("cuit", "El CUIT no es válido.")]
    [InlineData("condicion-iva", "La condición frente al IVA no es válida.")]
    [InlineData("nombre", "El nombre del solicitante es obligatorio.")]
    [InlineData("email", "El email no es válido.")]
    [InlineData("telefono-corto", "El teléfono no es válido.")]
    [InlineData("telefono-caracteres", "El teléfono no es válido.")]
    [InlineData("domicilio", "El domicilio es obligatorio.")]
    [InlineData("ciudad", "La ciudad es obligatoria.")]
    [InlineData("provincia", "La provincia es obligatoria.")]
    [InlineData("codigo-postal-corto", "El código postal no es válido.")]
    [InlineData("codigo-postal-caracteres", "El código postal no es válido.")]
    [InlineData("mensaje", "El mensaje es demasiado largo.")]
    [InlineData("terminos", "Tenés que aceptar los términos para enviar la solicitud.")]
    public async Task CrearAsync_ConDatosInvalidos_LanzaValidacionYNoInsertaNiEncola(
        string caso,
        string mensajeEsperado)
    {
        var contexto = new Contexto();
        var request = Request();
        request = caso switch
        {
            "razon-social-vacia" => request with { RazonSocial = "   " },
            "razon-social-corta" => request with { RazonSocial = "AB" },
            "cuit" => request with { Cuit = "20123456787" },
            "condicion-iva" => request with { CondicionIva = 99 },
            "nombre" => request with { NombreSolicitante = "A" },
            "email" => request with { Email = "sin-arroba" },
            "telefono-corto" => request with { Telefono = "1234567" },
            "telefono-caracteres" => request with { Telefono = "1234ABCD" },
            "domicilio" => request with { Domicilio = "abc" },
            "ciudad" => request with { Ciudad = "A" },
            "provincia" => request with { Provincia = "A" },
            "codigo-postal-corto" => request with { CodigoPostal = "123" },
            "codigo-postal-caracteres" => request with { CodigoPostal = "1234-5" },
            "mensaje" => request with { Mensaje = new string('a', 1001) },
            "terminos" => request with { AceptaTerminos = false },
            _ => throw new ArgumentOutOfRangeException(nameof(caso), caso, "Caso desconocido.")
        };

        var excepcion = await Assert.ThrowsAsync<SolicitudAltaValidationException>(
            () => contexto.Service.CrearAsync(request));

        Assert.Equal(mensajeEsperado, excepcion.Message);
        Assert.Empty(contexto.Solicitudes.Insertadas);
        Assert.Empty(contexto.Outbox.Encolados);
        Assert.Empty(contexto.Eventos.Eventos);
    }

    [Fact]
    public async Task CrearAsync_ConEmailPendiente_LanzaSolicitudPendienteYNoInserta()
    {
        var contexto = new Contexto();
        contexto.Solicitudes.EmailsPendientes.Add(EmailValido);

        await Assert.ThrowsAsync<SolicitudPendienteException>(
            () => contexto.Service.CrearAsync(Request()));

        Assert.Empty(contexto.Solicitudes.Insertadas);
        Assert.Empty(contexto.Outbox.Encolados);
        Assert.Empty(contexto.Eventos.Eventos);
    }

    [Fact]
    public async Task CrearAsync_ConCuitPendiente_LanzaSolicitudPendienteYNoInserta()
    {
        var contexto = new Contexto();
        contexto.Solicitudes.CuitsPendientes.Add(CuitValido);

        await Assert.ThrowsAsync<SolicitudPendienteException>(
            () => contexto.Service.CrearAsync(Request()));

        Assert.Empty(contexto.Solicitudes.Insertadas);
        Assert.Empty(contexto.Outbox.Encolados);
        Assert.Empty(contexto.Eventos.Eventos);
    }

    [Fact]
    public async Task CrearAsync_ConCuitDeContribuyente_DevuelveRespuestaGenericaYAuditaSinInsertar()
    {
        var contexto = new Contexto();
        contexto.Contribuyentes.Cuits.Add(CuitValido);

        var respuesta = await contexto.Service.CrearAsync(Request());

        Assert.Equal("SOLICITUD_RECIBIDA", respuesta.Codigo);
        Assert.Equal(CrearSolicitudAltaService.MensajeSolicitudRecibida, respuesta.Mensaje);
        Assert.Empty(contexto.Solicitudes.Insertadas);
        Assert.Empty(contexto.Outbox.Encolados);

        var evento = Assert.Single(contexto.Eventos.Eventos);
        Assert.Equal("solicitud.cuit_registrado", evento.Accion);
        Assert.Equal(ActorTipos.Sistema, evento.ActorTipo);
        Assert.Equal("SolicitudAltaEmpresa", evento.Entidad);
        Assert.Contains(CuitValido, evento.Datos);
    }

    [Fact]
    public async Task CrearAsync_SiElEncoladoFalla_IgualPersisteYResponde()
    {
        var contexto = new Contexto();
        contexto.Outbox.Excepcion = new InvalidOperationException("outbox caída.");

        var respuesta = await contexto.Service.CrearAsync(Request());

        Assert.Equal("SOLICITUD_RECIBIDA", respuesta.Codigo);
        Assert.Single(contexto.Solicitudes.Insertadas);
        Assert.Empty(contexto.Outbox.Encolados);
        Assert.Contains(AccionesAuditoria.SolicitudCreada, contexto.Eventos.Acciones);
    }

    [Fact]
    public async Task CrearAsync_ConAdministracionConfigurada_EncolaLaNotificacionAlAdministrador()
    {
        var contexto = new Contexto(adminNotificationEmail: "admin@facturar.com");

        await contexto.Service.CrearAsync(Request());

        var solicitud = Assert.Single(contexto.Solicitudes.Insertadas);
        Assert.Equal(2, contexto.Outbox.Encolados.Count);
        Assert.Contains(
            contexto.Outbox.Encolados,
            m => m.Tipo == TiposMensajeOutbox.EmailSolicitudRecibida);
        Assert.Contains(
            contexto.Outbox.Encolados,
            m => m.Tipo == TiposMensajeOutbox.EmailNuevaSolicitud);
        Assert.All(
            contexto.Outbox.Encolados,
            m => Assert.Equal(solicitud.Id, PayloadEmailSolicitud.Leer(m.Payload)));
    }

    [Fact]
    public async Task CrearAsync_SinAdministracionConfigurada_NoEncolaLaNotificacionAlAdministrador()
    {
        var contexto = new Contexto(adminNotificationEmail: null);

        await contexto.Service.CrearAsync(Request());

        var mensaje = Assert.Single(contexto.Outbox.Encolados);
        Assert.Equal(TiposMensajeOutbox.EmailSolicitudRecibida, mensaje.Tipo);
    }

    [Fact]
    public async Task CrearAsync_ConDatosPersonales_LaAuditoriaYElPayloadNoLosGuardan()
    {
        var contexto = new Contexto();
        var request = Request() with
        {
            NombreSolicitante = "Nombre Muy Sensible",
            Email = "persona.sensible@demo.com",
            Telefono = "+54 11 5555-4444",
            Domicilio = "Domicilio Secreto 123",
            Mensaje = "Mensaje privado del solicitante"
        };

        await contexto.Service.CrearAsync(request);

        Assert.NotEmpty(contexto.Eventos.Eventos);
        foreach (var evento in contexto.Eventos.Eventos)
        {
            var datos = evento.Datos ?? string.Empty;
            Assert.Contains(CuitValido, datos);
            Assert.DoesNotContain("persona.sensible@demo.com", datos);
            Assert.DoesNotContain("Nombre Muy Sensible", datos);
            Assert.DoesNotContain("5555", datos);
            Assert.DoesNotContain("Domicilio Secreto", datos);
            Assert.DoesNotContain("Mensaje privado", datos);
        }

        Assert.NotEmpty(contexto.Outbox.Encolados);
        foreach (var mensaje in contexto.Outbox.Encolados)
        {
            Assert.DoesNotContain("persona.sensible@demo.com", mensaje.Payload);
            Assert.DoesNotContain("Nombre Muy Sensible", mensaje.Payload);
            Assert.DoesNotContain("5555", mensaje.Payload);
            Assert.DoesNotContain("Domicilio Secreto", mensaje.Payload);
            Assert.DoesNotContain("Mensaje privado", mensaje.Payload);
        }
    }

    [Fact]
    public async Task CrearAsync_SiLaAuditoriaFalla_IgualPersisteYEncola()
    {
        var contexto = new Contexto();
        contexto.Eventos.Excepcion = new InvalidOperationException("auditoría caída.");

        var respuesta = await contexto.Service.CrearAsync(Request());

        Assert.Equal("SOLICITUD_RECIBIDA", respuesta.Codigo);
        Assert.Single(contexto.Solicitudes.Insertadas);
        Assert.Single(contexto.Outbox.Encolados);
    }

    private static SolicitudAltaRequest Request() => new(
        RazonSocial: "ACME S.A.",
        Cuit: CuitValido,
        CondicionIva: (int)CondicionIva.ResponsableInscripto,
        NombreSolicitante: "Ana Pérez",
        Email: EmailValido,
        Telefono: "+54 11 5555-4444",
        Domicilio: "Av. Corrientes 1234",
        Ciudad: "Buenos Aires",
        Provincia: "CABA",
        CodigoPostal: "C1043",
        Mensaje: null,
        AceptaTerminos: true);

    private sealed class Contexto
    {
        public Contexto(string? adminNotificationEmail = null)
        {
            Service = new CrearSolicitudAltaService(
                Solicitudes,
                Contribuyentes,
                Eventos,
                Outbox,
                new NotificacionesOptions { AdminNotificationEmail = adminNotificationEmail },
                NullLogger<CrearSolicitudAltaService>.Instance);
        }

        public FakeSolicitudAltaRepository Solicitudes { get; } = new();

        public FakeContribuyenteRepository Contribuyentes { get; } = new();

        public FakeEventoAuditoriaRepository Eventos { get; } = new();

        public FakeOutboxRepository Outbox { get; } = new();

        public CrearSolicitudAltaService Service { get; }
    }

    private sealed class FakeSolicitudAltaRepository : ISolicitudAltaRepository
    {
        public List<SolicitudAltaEmpresa> Insertadas { get; } = [];

        public HashSet<string> CuitsPendientes { get; } = new(StringComparer.Ordinal);

        public HashSet<string> EmailsPendientes { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Task InsertarAsync(SolicitudAltaEmpresa solicitud, CancellationToken ct = default)
        {
            Insertadas.Add(solicitud);
            return Task.CompletedTask;
        }

        public Task<bool> ExistePendientePorCuitAsync(string cuit, CancellationToken ct = default)
            => Task.FromResult(CuitsPendientes.Contains(cuit));

        public Task<bool> ExistePendientePorEmailAsync(string email, CancellationToken ct = default)
            => Task.FromResult(EmailsPendientes.Contains(email));

        public Task<SolicitudAltaEmpresa?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default)
            => throw new NotSupportedException();

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

    private sealed class FakeContribuyenteRepository : IContribuyenteRepository
    {
        public HashSet<string> Cuits { get; } = new(StringComparer.Ordinal);

        public Task<Contribuyente?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<Contribuyente?>(null);

        public Task<bool> ExistePorCuitAsync(string cuit, CancellationToken ct = default)
            => Task.FromResult(Cuits.Contains(cuit));

        public Task InsertarAsync(Contribuyente contribuyente, CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    private sealed class FakeEventoAuditoriaRepository : IEventoAuditoriaRepository
    {
        public List<EventoAuditoria> Eventos { get; } = [];

        public Exception? Excepcion { get; set; }

        public IReadOnlyList<string> Acciones => Eventos.Select(e => e.Accion).ToList();

        public Task AddAsync(EventoAuditoria evento, CancellationToken ct = default)
        {
            if (Excepcion is not null)
            {
                throw Excepcion;
            }

            Eventos.Add(evento);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<EventoAuditoria>> ListarPorEntidadAsync(
            string entidad,
            string entidadId,
            CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    private sealed class FakeOutboxRepository : IOutboxRepository
    {
        public List<OutboxMessage> Encolados { get; } = [];

        public Exception? Excepcion { get; set; }

        public Task<OutboxMessage> EncolarAsync(
            string tipo,
            string payload,
            CancellationToken ct = default)
        {
            if (Excepcion is not null)
            {
                throw Excepcion;
            }

            var mensaje = new OutboxMessage { Tipo = tipo, Payload = payload };
            Encolados.Add(mensaje);
            return Task.FromResult(mensaje);
        }

        public Task<IReadOnlyList<OutboxMessage>> ReclamarPendientesAsync(
            int lote,
            IReadOnlyCollection<Guid>? excluir = null,
            CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task MarcarProcesadoAsync(Guid id, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task MarcarFallidoAsync(Guid id, string error, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task DevolverAPendienteAsync(Guid id, string? error, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<OutboxMessage?> ConsultarAsync(Guid id, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<OutboxMessage>> ListarPorEstadoAsync(
            string estado,
            DateTime? desde,
            DateTime? hasta,
            int limite,
            CancellationToken ct = default)
            => throw new NotSupportedException();
    }
}
