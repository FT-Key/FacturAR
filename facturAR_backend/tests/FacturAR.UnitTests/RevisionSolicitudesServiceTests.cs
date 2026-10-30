using System.Text.Json;
using FacturAR.Application.Abstractions;
using FacturAR.Application.Notificaciones;
using FacturAR.Application.Solicitudes;
using FacturAR.Contracts.Solicitudes;
using FacturAR.Domain.Entities;
using FacturAR.Domain.Enums;
using FacturAR.Domain.Exceptions;
using FacturAR.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;

namespace FacturAR.UnitTests;

public sealed class RevisionSolicitudesServiceTests
{
    private const string CuitValido = "20123456786";
    private const string EmailSolicitante = "solicitante@demo.com";
    private static readonly Guid AdministradorId = Guid.NewGuid();

    [Fact]
    public async Task Aprobar_SolicitudPendiente_CreaEmpresaPuntoDeVentaEInvitacionYEnviaEmail()
    {
        var ctx = new Contexto();
        var solicitud = ctx.AgregarSolicitud();
        var antes = DateTime.UtcNow;

        var respuesta = await ctx.Aprobar.AprobarAsync(solicitud.Id, AdministradorId);

        Assert.True(respuesta.EmailEnviado);
        Assert.Null(respuesta.AdvertenciaEmail);

        var contribuyente = Assert.Single(ctx.Contribuyentes.Insertados);
        Assert.Equal(respuesta.ContribuyenteId, contribuyente.Id);
        Assert.Equal(CuitValido, contribuyente.Cuit);
        Assert.Equal(solicitud.RazonSocial, contribuyente.RazonSocial);
        Assert.Equal(EmailSolicitante, contribuyente.Email);
        Assert.True(contribuyente.Activo);

        var puntoVenta = Assert.Single(ctx.PuntosVenta.Insertados);
        Assert.Equal(contribuyente.Id, puntoVenta.ContribuyenteId);
        Assert.Equal(1, puntoVenta.Numero);
        Assert.Equal("Casa Central", puntoVenta.Descripcion);
        Assert.True(puntoVenta.Activo);

        var invitacion = Assert.Single(ctx.Invitaciones.Todas);
        Assert.Equal(respuesta.InvitacionId, invitacion.Id);
        Assert.Equal(contribuyente.Id, invitacion.ContribuyenteId);
        Assert.Equal(solicitud.Id, invitacion.SolicitudId);
        Assert.Equal(EmailSolicitante, invitacion.Email);
        Assert.Equal(RolUsuario.Administrador, invitacion.Rol);
        Assert.Equal(EstadoInvitacion.Pendiente, invitacion.Estado);
        Assert.Equal(AdministradorId, invitacion.CreadaPorAdministradorId);
        Assert.Equal(64, invitacion.TokenHash.Length);
        Assert.Equal(8, invitacion.TokenPrefix.Length);
        Assert.InRange(invitacion.ExpiraAt, antes.AddHours(48), DateTime.UtcNow.AddHours(48));
        Assert.Equal(1, invitacion.IntentosEnvio);

        Assert.Equal(EstadoSolicitud.Aprobada, solicitud.Estado);
        Assert.Equal(contribuyente.Id, solicitud.ContribuyenteId);
        Assert.Equal(invitacion.Id, solicitud.InvitacionId);
        Assert.Equal(AdministradorId, solicitud.RevisadaPorAdministradorId);
        Assert.NotNull(solicitud.RevisadaAt);

        var email = Assert.Single(ctx.Emails.Enviados);
        Assert.Equal(EmailSolicitante, email.Para);
        Assert.Equal(PlantillasEmail.NombreSolicitudAprobada, email.Plantilla);
        var token = TokenEnLink(email.CuerpoTexto);
        Assert.Equal(invitacion.TokenHash, TokenInvitacion.CalcularHash(token));
        Assert.StartsWith(invitacion.TokenPrefix, token);

        var aprobada = Assert.Single(ctx.Eventos.Eventos, e => e.Accion == "solicitud.aprobada");
        Assert.Equal(AdministradorId, aprobada.AdministradorId);
        Assert.Equal("Administrador", aprobada.ActorTipo);
        Assert.Null(aprobada.ContribuyenteId);
        Assert.Equal("SolicitudAltaEmpresa", aprobada.Entidad);
        Assert.Equal(solicitud.Id.ToString(), aprobada.EntidadId);
        Assert.Equal(CuitValido, Datos(aprobada)["cuit"]);
        Assert.Contains(ctx.Eventos.Eventos, e => e.Accion == "email.enviado");
        Assert.DoesNotContain(ctx.Eventos.Eventos, e => (e.Datos ?? string.Empty).Contains(token, StringComparison.Ordinal));
        Assert.Equal(1, ctx.Transaccion.Ejecuciones);
    }

    [Theory]
    [InlineData(EstadoSolicitud.Aprobada)]
    [InlineData(EstadoSolicitud.Rechazada)]
    public async Task Aprobar_SolicitudYaResuelta_LanzaYaProcesadaSinCrearNada(EstadoSolicitud estado)
    {
        var ctx = new Contexto();
        var solicitud = ctx.AgregarSolicitud(estado);

        await Assert.ThrowsAsync<SolicitudYaProcesadaException>(
            () => ctx.Aprobar.AprobarAsync(solicitud.Id, AdministradorId));

        ctx.AssertSinEmpresaNiEmail();
    }

    [Fact]
    public async Task Aprobar_CuitDeOtroContribuyente_LanzaCuitYaRegistradoYDejaPendiente()
    {
        var ctx = new Contexto();
        var solicitud = ctx.AgregarSolicitud();
        ctx.Contribuyentes.CuitsExistentes.Add(CuitValido);

        await Assert.ThrowsAsync<CuitYaRegistradoException>(
            () => ctx.Aprobar.AprobarAsync(solicitud.Id, AdministradorId));

        Assert.Equal(EstadoSolicitud.Pendiente, solicitud.Estado);
        ctx.AssertSinEmpresaNiEmail();
    }

    [Fact]
    public async Task Aprobar_SolicitudInexistente_LanzaNoEncontrada()
    {
        var ctx = new Contexto();

        await Assert.ThrowsAsync<SolicitudNoEncontradaException>(
            () => ctx.Aprobar.AprobarAsync(Guid.NewGuid(), AdministradorId));
    }

    [Fact]
    public async Task Aprobar_CuitInvalidoEnLaSolicitud_LanzaValidacion()
    {
        var ctx = new Contexto();
        var solicitud = ctx.AgregarSolicitud();
        solicitud.Cuit = "20123456780";

        var ex = await Assert.ThrowsAsync<SolicitudAltaValidationException>(
            () => ctx.Aprobar.AprobarAsync(solicitud.Id, AdministradorId));

        Assert.Equal("El CUIT de la solicitud no es válido.", ex.Message);
        ctx.AssertSinEmpresaNiEmail();
    }

    [Fact]
    public async Task Aprobar_ConEmailCaido_NoRevierteYDevuelveAdvertencia()
    {
        var ctx = new Contexto();
        var solicitud = ctx.AgregarSolicitud();
        ctx.Emails.Excepcion = new InvalidOperationException("SMTP caído");

        var respuesta = await ctx.Aprobar.AprobarAsync(solicitud.Id, AdministradorId);

        Assert.False(respuesta.EmailEnviado);
        Assert.Equal(AprobarSolicitudService.AdvertenciaEmail, respuesta.AdvertenciaEmail);
        Assert.Single(ctx.Contribuyentes.Insertados);
        Assert.Single(ctx.PuntosVenta.Insertados);
        Assert.Single(ctx.Invitaciones.Todas);
        Assert.Equal(EstadoSolicitud.Aprobada, solicitud.Estado);
        Assert.Contains(ctx.Eventos.Eventos, e => e.Accion == "email.fallido");
    }

    [Fact]
    public async Task Rechazar_ConMotivo_DejaRechazadaYEnviaElMotivoSinCrearEmpresa()
    {
        var ctx = new Contexto();
        var solicitud = ctx.AgregarSolicitud();

        var respuesta = await ctx.Rechazar.RechazarAsync(solicitud.Id, "  Datos fiscales incompletos  ", AdministradorId);

        Assert.Equal("Rechazada", respuesta.Estado);
        Assert.True(respuesta.EmailEnviado);
        Assert.Equal(EstadoSolicitud.Rechazada, solicitud.Estado);
        Assert.Equal("Datos fiscales incompletos", solicitud.MotivoRechazo);
        Assert.Equal(AdministradorId, solicitud.RevisadaPorAdministradorId);

        var email = Assert.Single(ctx.Emails.Enviados);
        Assert.Equal(PlantillasEmail.NombreSolicitudRechazada, email.Plantilla);
        Assert.Contains("Datos fiscales incompletos", email.CuerpoTexto);

        Assert.Empty(ctx.Contribuyentes.Insertados);
        Assert.Empty(ctx.PuntosVenta.Insertados);
        Assert.Empty(ctx.Invitaciones.Todas);

        var evento = Assert.Single(ctx.Eventos.Eventos, e => e.Accion == "solicitud.rechazada");
        Assert.Equal(AdministradorId, evento.AdministradorId);
        Assert.Equal("Datos fiscales incompletos", Datos(evento)["motivo"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Corto")]
    public async Task Rechazar_SinMotivoValido_LanzaValidacionYDejaPendiente(string? motivo)
    {
        var ctx = new Contexto();
        var solicitud = ctx.AgregarSolicitud();

        var ex = await Assert.ThrowsAsync<SolicitudAltaValidationException>(
            () => ctx.Rechazar.RechazarAsync(solicitud.Id, motivo, AdministradorId));

        Assert.Equal(RechazarSolicitudService.MensajeMotivoObligatorio, ex.Message);
        Assert.Equal(EstadoSolicitud.Pendiente, solicitud.Estado);
        Assert.Empty(ctx.Emails.Enviados);
    }

    [Fact]
    public async Task Rechazar_SolicitudAprobada_LanzaYaProcesada()
    {
        var ctx = new Contexto();
        var solicitud = ctx.AgregarSolicitud(EstadoSolicitud.Aprobada);

        await Assert.ThrowsAsync<SolicitudYaProcesadaException>(
            () => ctx.Rechazar.RechazarAsync(solicitud.Id, "Datos fiscales incompletos", AdministradorId));

        Assert.Empty(ctx.Emails.Enviados);
    }

    [Fact]
    public async Task Reenviar_RevocaLaAnteriorYCreaOtraConTokenYVencimientoNuevos()
    {
        var ctx = new Contexto();
        var solicitud = ctx.AgregarSolicitud();
        await ctx.Aprobar.AprobarAsync(solicitud.Id, AdministradorId);
        var anterior = Assert.Single(ctx.Invitaciones.Todas);
        anterior.ExpiraAt = DateTime.UtcNow.AddHours(-1);
        ctx.Emails.Enviados.Clear();
        var soporte = Guid.NewGuid();

        var respuesta = await ctx.Reenviar.ReenviarAsync(solicitud.Id, soporte);

        Assert.True(respuesta.EmailEnviado);
        Assert.Equal(EstadoInvitacion.Revocada, anterior.Estado);
        Assert.NotNull(anterior.RevocadaAt);

        var nueva = Assert.Single(ctx.Invitaciones.Todas, i => i.Id == respuesta.InvitacionId);
        Assert.Equal(EstadoInvitacion.Pendiente, nueva.Estado);
        Assert.NotEqual(anterior.TokenHash, nueva.TokenHash);
        Assert.True(nueva.ExpiraAt > DateTime.UtcNow.AddHours(47));
        Assert.Equal(soporte, nueva.CreadaPorAdministradorId);
        Assert.Equal(anterior.ContribuyenteId, nueva.ContribuyenteId);

        var email = Assert.Single(ctx.Emails.Enviados);
        Assert.Equal(PlantillasEmail.NombreInvitacionReenviada, email.Plantilla);
        Assert.Equal(nueva.TokenHash, TokenInvitacion.CalcularHash(TokenEnLink(email.CuerpoTexto)));
        Assert.Contains(ctx.Eventos.Eventos, e => e.Accion == "invitacion.reenviada" && e.AdministradorId == soporte);
    }

    [Fact]
    public async Task Reenviar_ConEmailCaido_DevuelveEmailNoEnviadoYDejaLaNuevaVigente()
    {
        var ctx = new Contexto();
        var solicitud = ctx.AgregarSolicitud();
        await ctx.Aprobar.AprobarAsync(solicitud.Id, AdministradorId);
        ctx.Emails.Excepcion = new InvalidOperationException("SMTP caído");

        var respuesta = await ctx.Reenviar.ReenviarAsync(solicitud.Id, AdministradorId);

        Assert.False(respuesta.EmailEnviado);
        Assert.Equal(ReenviarInvitacionService.AdvertenciaEmail, respuesta.AdvertenciaEmail);
        Assert.Equal(EstadoInvitacion.Pendiente, ctx.Invitaciones.Todas.Single(i => i.Id == respuesta.InvitacionId).Estado);
    }

    [Theory]
    [InlineData(EstadoSolicitud.Pendiente)]
    [InlineData(EstadoSolicitud.Rechazada)]
    public async Task Reenviar_SinInvitacionVigente_LanzaInvitacionNoEncontrada(EstadoSolicitud estado)
    {
        var ctx = new Contexto();
        var solicitud = ctx.AgregarSolicitud(estado);

        await Assert.ThrowsAsync<InvitacionNoEncontradaException>(
            () => ctx.Reenviar.ReenviarAsync(solicitud.Id, AdministradorId));
    }

    [Fact]
    public async Task Reenviar_InvitacionYaUsada_LanzaInvitacionNoEncontrada()
    {
        var ctx = new Contexto();
        var solicitud = ctx.AgregarSolicitud();
        await ctx.Aprobar.AprobarAsync(solicitud.Id, AdministradorId);
        ctx.Invitaciones.Todas.Single().Estado = EstadoInvitacion.Usada;

        await Assert.ThrowsAsync<InvitacionNoEncontradaException>(
            () => ctx.Reenviar.ReenviarAsync(solicitud.Id, AdministradorId));
    }

    [Fact]
    public async Task Editar_SolicitudPendiente_ActualizaLosCincoCamposYAudita()
    {
        var ctx = new Contexto();
        var solicitud = ctx.AgregarSolicitud();
        var actualizadaAntes = solicitud.UpdatedAt;

        await ctx.Editar.EditarAsync(
            solicitud.Id,
            new EditarSolicitudRequest("  Nueva Razón SA ", (int)CondicionIva.Monotributo, "Nuevo@Demo.com", "+54 381 555-1234", "Calle Falsa 123"),
            AdministradorId);

        Assert.Equal("Nueva Razón SA", solicitud.RazonSocial);
        Assert.Equal(CondicionIva.Monotributo, solicitud.CondicionIva);
        Assert.Equal("nuevo@demo.com", solicitud.EmailSolicitante);
        Assert.Equal("+54 381 555-1234", solicitud.Telefono);
        Assert.Equal("Calle Falsa 123", solicitud.Domicilio);
        Assert.Equal(CuitValido, solicitud.Cuit);
        Assert.Equal(EstadoSolicitud.Pendiente, solicitud.Estado);
        Assert.True(solicitud.UpdatedAt >= actualizadaAntes);

        var evento = Assert.Single(ctx.Eventos.Eventos, e => e.Accion == "solicitud.editada");
        Assert.Equal(AdministradorId, evento.AdministradorId);
    }

    [Fact]
    public async Task Editar_SolicitudAprobada_LanzaYaProcesadaSinCambiar()
    {
        var ctx = new Contexto();
        var solicitud = ctx.AgregarSolicitud(EstadoSolicitud.Aprobada);
        var razonSocial = solicitud.RazonSocial;

        await Assert.ThrowsAsync<SolicitudYaProcesadaException>(() => ctx.Editar.EditarAsync(
            solicitud.Id,
            new EditarSolicitudRequest("Otra Razón SA", 1, EmailSolicitante, "3815551234", "Calle Falsa 123"),
            AdministradorId));

        Assert.Equal(razonSocial, solicitud.RazonSocial);
    }

    [Theory]
    [InlineData("AB", 1, EmailSolicitante, "3815551234", "Calle Falsa 123", "La razón social debe tener entre 3 y 200 caracteres.")]
    [InlineData("ACME SA", 99, EmailSolicitante, "3815551234", "Calle Falsa 123", "La condición frente al IVA no es válida.")]
    [InlineData("ACME SA", 1, "no-es-email", "3815551234", "Calle Falsa 123", "El email no es válido.")]
    [InlineData("ACME SA", 1, EmailSolicitante, "123", "Calle Falsa 123", "El teléfono no es válido.")]
    [InlineData("ACME SA", 1, EmailSolicitante, "3815551234", "Ab", "El domicilio es obligatorio.")]
    public async Task Editar_ConDatosInvalidos_LanzaValidacion(
        string razonSocial,
        int condicionIva,
        string email,
        string telefono,
        string domicilio,
        string mensaje)
    {
        var ctx = new Contexto();
        var solicitud = ctx.AgregarSolicitud();

        var ex = await Assert.ThrowsAsync<SolicitudAltaValidationException>(() => ctx.Editar.EditarAsync(
            solicitud.Id,
            new EditarSolicitudRequest(razonSocial, condicionIva, email, telefono, domicilio),
            AdministradorId));

        Assert.Equal(mensaje, ex.Message);
    }

    [Theory]
    [InlineData(0, 20, null)]
    [InlineData(1, 0, null)]
    [InlineData(1, 101, null)]
    [InlineData(1, 20, "Borrador")]
    [InlineData(1, 20, "2")]
    public async Task Listar_ConParametrosInvalidos_LanzaValidacion(int page, int pageSize, string? estado)
    {
        var ctx = new Contexto();

        await Assert.ThrowsAsync<SolicitudAltaValidationException>(
            () => ctx.Listar.ListarAsync(page, pageSize, estado, null, null, null));
    }

    [Fact]
    public async Task Listar_NormalizaFiltrosYCalculaPaginas()
    {
        var ctx = new Contexto();
        for (var i = 0; i < 3; i++)
        {
            ctx.AgregarSolicitud();
        }

        var pagina = await ctx.Listar.ListarAsync(1, 2, "pendiente", "20-1234", "  acme ", " Solicitante@ ");

        Assert.Equal(3, pagina.Total);
        Assert.Equal(2, pagina.TotalPages);
        Assert.Equal(2, pagina.Items.Count);
        Assert.Equal(EstadoSolicitud.Pendiente, ctx.Solicitudes.UltimoFiltro!.Estado);
        Assert.Equal("201234", ctx.Solicitudes.UltimoFiltro.Cuit);
        Assert.Equal("acme", ctx.Solicitudes.UltimoFiltro.RazonSocial);
        Assert.Equal("solicitante@", ctx.Solicitudes.UltimoFiltro.Email);
    }

    [Fact]
    public async Task Obtener_SolicitudAprobada_IncluyeInvitacionVigenteEHistorial()
    {
        var ctx = new Contexto();
        var solicitud = ctx.AgregarSolicitud();
        await ctx.Aprobar.AprobarAsync(solicitud.Id, AdministradorId);

        var detalle = await ctx.Obtener.ObtenerAsync(solicitud.Id);

        Assert.Equal("Aprobada", detalle.Estado);
        Assert.NotNull(detalle.Invitacion);
        Assert.Equal("Pendiente", detalle.Invitacion.Estado);
        Assert.True(detalle.Invitacion.PuedeReenviarse);
        Assert.Contains(detalle.Historial, e => e.Accion == "solicitud.aprobada");
    }

    [Fact]
    public void TokenInvitacion_GeneraTokenAleatorioConHashSha256PrefijoYVencimientoA48Horas()
    {
        var ahora = new DateTime(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);

        var primero = TokenInvitacion.Generar(ahora);
        var segundo = TokenInvitacion.Generar(ahora);

        Assert.NotEqual(primero.Token, segundo.Token);
        Assert.Equal(43, primero.Token.Length);
        Assert.DoesNotContain('+', primero.Token);
        Assert.DoesNotContain('/', primero.Token);
        Assert.Equal(64, primero.Hash.Length);
        Assert.Equal(primero.Hash, primero.Hash.ToLowerInvariant());
        Assert.Equal(TokenInvitacion.CalcularHash(primero.Token), primero.Hash);
        Assert.Equal(primero.Token[..8], primero.Prefix);
        Assert.Equal(ahora.AddHours(48), primero.ExpiraAt);
        Assert.DoesNotContain(primero.Token, primero.ToString());
    }

    [Fact]
    public void Invitacion_PendienteVencida_SeInformaComoVencida()
    {
        var invitacion = new Invitacion { Estado = EstadoInvitacion.Pendiente, ExpiraAt = DateTime.UtcNow.AddMinutes(-1) };

        Assert.Equal(EstadoInvitacion.Vencida, invitacion.EstadoEfectivo(DateTime.UtcNow));
    }

    private static string TokenEnLink(string texto)
    {
        const string marcador = "/activar?token=";
        var inicio = texto.IndexOf(marcador, StringComparison.Ordinal) + marcador.Length;
        var fin = texto.IndexOfAny(['\r', '\n'], inicio);
        return Uri.UnescapeDataString(fin < 0 ? texto[inicio..] : texto[inicio..fin]);
    }

    private sealed class Contexto
    {
        public Contexto()
        {
            var email = new EmailService(Emails, new NotificacionesOptions { PortalBaseUrl = "https://portal.test" });
            var notificador = new NotificadorRevisionSolicitudes(
                Eventos,
                Invitaciones,
                NullLogger<NotificadorRevisionSolicitudes>.Instance);

            Aprobar = new AprobarSolicitudService(
                Solicitudes, Contribuyentes, PuntosVenta, Invitaciones, Eventos, Transaccion, email, notificador);
            Rechazar = new RechazarSolicitudService(Solicitudes, Eventos, Transaccion, email, notificador);
            Reenviar = new ReenviarInvitacionService(Solicitudes, Invitaciones, Eventos, Transaccion, email, notificador);
            Editar = new EditarSolicitudService(Solicitudes, Eventos, Transaccion);
            Listar = new ListarSolicitudesService(Solicitudes);
            Obtener = new ObtenerSolicitudService(Solicitudes, Invitaciones, Eventos);
        }

        public FakeSolicitudes Solicitudes { get; } = new();
        public FakeContribuyentes Contribuyentes { get; } = new();
        public FakePuntosVenta PuntosVenta { get; } = new();
        public FakeInvitaciones Invitaciones { get; } = new();
        public FakeEventos Eventos { get; } = new();
        public FakeTransaccion Transaccion { get; } = new();
        public FakeEmailSender Emails { get; } = new();

        public AprobarSolicitudService Aprobar { get; }
        public RechazarSolicitudService Rechazar { get; }
        public ReenviarInvitacionService Reenviar { get; }
        public EditarSolicitudService Editar { get; }
        public ListarSolicitudesService Listar { get; }
        public ObtenerSolicitudService Obtener { get; }

        public SolicitudAltaEmpresa AgregarSolicitud(EstadoSolicitud estado = EstadoSolicitud.Pendiente)
        {
            var solicitud = new SolicitudAltaEmpresa
            {
                RazonSocial = "ACME S.A.",
                Cuit = CuitValido,
                CondicionIva = CondicionIva.ResponsableInscripto,
                NombreSolicitante = "Ana Pérez",
                EmailSolicitante = EmailSolicitante,
                Telefono = "3815551234",
                Domicilio = "San Martín 100",
                Ciudad = "Tucumán",
                Provincia = "Tucumán",
                CodigoPostal = "4000",
                AceptaTerminos = true,
                Estado = estado
            };
            Solicitudes.Todas.Add(solicitud);
            return solicitud;
        }

        public void AssertSinEmpresaNiEmail()
        {
            Assert.Empty(Contribuyentes.Insertados);
            Assert.Empty(PuntosVenta.Insertados);
            Assert.Empty(Invitaciones.Todas);
            Assert.Empty(Emails.Enviados);
        }
    }

    private sealed class FakeTransaccion : ITransaccionMongo
    {
        public int Ejecuciones { get; private set; }

        public Task<T> EjecutarAsync<T>(Func<CancellationToken, Task<T>> operacion, CancellationToken ct = default)
        {
            Ejecuciones++;
            return operacion(ct);
        }
    }

    private sealed class FakeSolicitudes : ISolicitudAltaRepository
    {
        public List<SolicitudAltaEmpresa> Todas { get; } = [];

        public FiltroSolicitudes? UltimoFiltro { get; private set; }

        public Task InsertarAsync(SolicitudAltaEmpresa solicitud, CancellationToken ct = default)
        {
            Todas.Add(solicitud);
            return Task.CompletedTask;
        }

        public Task<bool> ExistePendientePorCuitAsync(string cuit, CancellationToken ct = default)
            => Task.FromResult(Todas.Any(s => s.Cuit == cuit && s.Estado == EstadoSolicitud.Pendiente));

        public Task<bool> ExistePendientePorEmailAsync(string email, CancellationToken ct = default)
            => Task.FromResult(Todas.Any(s => s.EmailSolicitante == email && s.Estado == EstadoSolicitud.Pendiente));

        public Task<SolicitudAltaEmpresa?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(Todas.FirstOrDefault(s => s.Id == id));

        public Task<(IReadOnlyList<SolicitudAltaEmpresa> Items, long Total)> ListarAsync(
            FiltroSolicitudes filtro,
            CancellationToken ct = default)
        {
            UltimoFiltro = filtro;
            IReadOnlyList<SolicitudAltaEmpresa> items = Todas
                .Skip((filtro.Page - 1) * filtro.PageSize)
                .Take(filtro.PageSize)
                .ToList();
            return Task.FromResult((items, (long)Todas.Count));
        }

        public Task<bool> ActualizarDatosPendienteAsync(SolicitudAltaEmpresa solicitud, CancellationToken ct = default)
            => Task.FromResult(solicitud.Estado == EstadoSolicitud.Pendiente);

        public Task<bool> AprobarPendienteAsync(
            Guid id,
            Guid contribuyenteId,
            Guid invitacionId,
            Guid administradorId,
            DateTime revisadaAt,
            CancellationToken ct = default)
        {
            var solicitud = Todas.SingleOrDefault(s => s.Id == id && s.Estado == EstadoSolicitud.Pendiente);
            if (solicitud is null)
            {
                return Task.FromResult(false);
            }

            solicitud.Estado = EstadoSolicitud.Aprobada;
            solicitud.ContribuyenteId = contribuyenteId;
            solicitud.InvitacionId = invitacionId;
            solicitud.RevisadaPorAdministradorId = administradorId;
            solicitud.RevisadaAt = revisadaAt;
            return Task.FromResult(true);
        }

        public Task<bool> RechazarPendienteAsync(
            Guid id,
            string motivo,
            Guid administradorId,
            DateTime revisadaAt,
            CancellationToken ct = default)
        {
            var solicitud = Todas.SingleOrDefault(s => s.Id == id && s.Estado == EstadoSolicitud.Pendiente);
            if (solicitud is null)
            {
                return Task.FromResult(false);
            }

            solicitud.Estado = EstadoSolicitud.Rechazada;
            solicitud.MotivoRechazo = motivo;
            solicitud.RevisadaPorAdministradorId = administradorId;
            solicitud.RevisadaAt = revisadaAt;
            return Task.FromResult(true);
        }
    }

    private sealed class FakeContribuyentes : IContribuyenteRepository
    {
        public HashSet<string> CuitsExistentes { get; } = [];

        public List<Contribuyente> Insertados { get; } = [];

        public Task<Contribuyente?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(Insertados.FirstOrDefault(c => c.Id == id));

        public Task<bool> ExistePorCuitAsync(string cuit, CancellationToken ct = default)
            => Task.FromResult(CuitsExistentes.Contains(cuit) || Insertados.Any(c => c.Cuit == cuit));

        public Task InsertarAsync(Contribuyente contribuyente, CancellationToken ct = default)
        {
            Insertados.Add(contribuyente);
            return Task.CompletedTask;
        }
    }

    private sealed class FakePuntosVenta : IPuntoVentaRepository
    {
        public List<PuntoVenta> Insertados { get; } = [];

        public Task InsertarAsync(PuntoVenta puntoVenta, CancellationToken ct = default)
        {
            Insertados.Add(puntoVenta);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeInvitaciones : IInvitacionRepository
    {
        public List<Invitacion> Todas { get; } = [];

        public Task InsertarAsync(Invitacion invitacion, CancellationToken ct = default)
        {
            Todas.Add(invitacion);
            return Task.CompletedTask;
        }

        public Task<Invitacion?> ObtenerPorTokenHashAsync(string tokenHash, CancellationToken ct = default)
            => Task.FromResult(Todas.FirstOrDefault(i => i.TokenHash == tokenHash));

        public Task<Invitacion?> ObtenerVigentePorSolicitudAsync(Guid solicitudId, CancellationToken ct = default)
            => Task.FromResult(Todas
                .Where(i => i.SolicitudId == solicitudId && i.Estado == EstadoInvitacion.Pendiente)
                .OrderByDescending(i => i.CreatedAt)
                .FirstOrDefault());

        public Task<bool> RevocarAsync(Guid id, DateTime revocadaAt, CancellationToken ct = default)
        {
            var invitacion = Todas.SingleOrDefault(i => i.Id == id && i.Estado == EstadoInvitacion.Pendiente);
            if (invitacion is null)
            {
                return Task.FromResult(false);
            }

            invitacion.Estado = EstadoInvitacion.Revocada;
            invitacion.RevocadaAt = revocadaAt;
            return Task.FromResult(true);
        }

        public Task RegistrarEnvioAsync(Guid id, DateTime enviadoAt, CancellationToken ct = default)
        {
            var invitacion = Todas.Single(i => i.Id == id);
            invitacion.UltimoEnvioAt = enviadoAt;
            invitacion.IntentosEnvio++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeEventos : IEventoAuditoriaRepository
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
            => Task.FromResult<IReadOnlyList<EventoAuditoria>>(
                Eventos.Where(e => e.Entidad == entidad && e.EntidadId == entidadId).ToList());
    }

    private sealed class FakeEmailSender : IEmailSender
    {
        public List<EmailMessage> Enviados { get; } = [];

        public Exception? Excepcion { get; set; }

        public Task<EmailResult> EnviarAsync(EmailMessage mensaje, CancellationToken cancellationToken = default)
        {
            if (Excepcion is not null)
            {
                throw Excepcion;
            }

            Enviados.Add(mensaje);
            return Task.FromResult(EmailResult.Ok(DateTime.UtcNow));
        }
    }

    private static Dictionary<string, string> Datos(EventoAuditoria evento)
        => JsonSerializer.Deserialize<Dictionary<string, string>>(evento.Datos ?? "{}") ?? [];
}
