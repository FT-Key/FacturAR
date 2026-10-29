using FacturAR.Application.Notificaciones;
using FacturAR.Domain.Entities;

namespace FacturAR.UnitTests;

public sealed class EmailServiceTests
{
    [Fact]
    public async Task EnviarSolicitudRecibidaAsync_ComponeElMensajeParaElSolicitante()
    {
        var sender = new FakeEmailSender();
        var service = CrearService(sender);

        var resultado = await service.EnviarSolicitudRecibidaAsync(CrearSolicitud(), CancellationToken.None);

        Assert.True(resultado.Exitoso);
        var mensaje = Assert.Single(sender.Enviados);
        Assert.Equal("ana@acme.com", mensaje.Para);
        Assert.Equal("Recibimos tu solicitud de acceso a FacturAR", mensaje.Asunto);
        Assert.Equal(PlantillasEmail.NombreSolicitudRecibida, mensaje.Plantilla);
        Assert.Contains("ACME S.A.", mensaje.CuerpoHtml);
        Assert.Contains("\r\n", mensaje.CuerpoTexto);
    }

    [Fact]
    public async Task EnviarNuevaSolicitudAsync_SinAdminConfigurado_DevuelveFallidoSinEnviar()
    {
        var sender = new FakeEmailSender();
        var service = CrearService(sender, adminNotificationEmail: null);

        var resultado = await service.EnviarNuevaSolicitudAsync(CrearSolicitud(), CancellationToken.None);

        Assert.False(service.NotificacionAdministracionConfigurada);
        Assert.False(resultado.Exitoso);
        Assert.Equal("ADMIN_NOTIFICATION_EMAIL_NO_CONFIGURADA", resultado.Error);
        Assert.Empty(sender.Enviados);
    }

    [Fact]
    public async Task EnviarNuevaSolicitudAsync_ConAdminConfigurado_EnviaAlAdministrador()
    {
        var sender = new FakeEmailSender();
        var service = CrearService(sender, adminNotificationEmail: "admin@facturar.com");

        var resultado = await service.EnviarNuevaSolicitudAsync(CrearSolicitud(), CancellationToken.None);

        Assert.True(service.NotificacionAdministracionConfigurada);
        Assert.True(resultado.Exitoso);
        var mensaje = Assert.Single(sender.Enviados);
        Assert.Equal("admin@facturar.com", mensaje.Para);
        Assert.Equal("Nueva solicitud de alta: ACME S.A.", mensaje.Asunto);
        Assert.Contains("http://localhost:5116/admin/solicitudes", mensaje.CuerpoHtml);
    }

    [Fact]
    public async Task EnviarSolicitudAprobadaAsync_ArmaElLinkConLaBaseSinBarraFinal()
    {
        var sender = new FakeEmailSender();
        var service = CrearService(sender);
        var expiraAt = DateTime.UtcNow.AddHours(48);

        var resultado = await service.EnviarSolicitudAprobadaAsync(
            CrearSolicitud(),
            "abc123",
            expiraAt,
            CancellationToken.None);

        Assert.True(resultado.Exitoso);
        var mensaje = Assert.Single(sender.Enviados);
        Assert.Contains("http://localhost:5116/activar?token=abc123", mensaje.CuerpoHtml);
        Assert.Contains("http://localhost:5116/activar?token=abc123", mensaje.CuerpoTexto);
        Assert.Contains("48 horas", mensaje.CuerpoTexto);
    }

    [Fact]
    public async Task EnviarSolicitudRecibidaAsync_ConDestinatarioInvalido_DevuelveFallidoSinLlamarAlSender()
    {
        var sender = new FakeEmailSender();
        var service = CrearService(sender);
        var solicitud = CrearSolicitud();
        solicitud.EmailSolicitante = "no-es-email";

        var resultado = await service.EnviarSolicitudRecibidaAsync(solicitud, CancellationToken.None);

        Assert.False(resultado.Exitoso);
        Assert.NotNull(resultado.Error);
        Assert.Empty(sender.Enviados);
    }

    [Fact]
    public async Task EnviarSolicitudRecibidaAsync_SiElSenderFalla_DevuelveFallidoSinPropagar()
    {
        var sender = new FakeEmailSender
        {
            Excepcion = new InvalidOperationException("fallo token super-secreto")
        };
        var service = CrearService(sender);

        var resultado = await service.EnviarSolicitudRecibidaAsync(CrearSolicitud(), CancellationToken.None);

        Assert.False(resultado.Exitoso);
        Assert.NotNull(resultado.Error);
        Assert.Contains("InvalidOperationException", resultado.Error);
        Assert.DoesNotContain("super-secreto", resultado.Error);
    }

    [Fact]
    public async Task EnviarInvitacionReenviadaAsync_ConTokenCancelado_PropagaLaCancelacion()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();
        var sender = new FakeEmailSender
        {
            Excepcion = new OperationCanceledException(cancellationTokenSource.Token)
        };
        var service = CrearService(sender);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.EnviarInvitacionReenviadaAsync(
                CrearSolicitud(),
                "abc123",
                DateTime.UtcNow.AddHours(48),
                cancellationTokenSource.Token));
    }

    [Fact]
    public async Task EnviarInvitacionReenviadaAsync_ConCancelacionSinTokenCancelado_DevuelveFallido()
    {
        var sender = new FakeEmailSender
        {
            Excepcion = new OperationCanceledException()
        };
        var service = CrearService(sender);

        var resultado = await service.EnviarInvitacionReenviadaAsync(
            CrearSolicitud(),
            "abc123",
            DateTime.UtcNow.AddHours(48),
            CancellationToken.None);

        Assert.False(resultado.Exitoso);
        Assert.NotNull(resultado.Error);
        Assert.Contains("OperationCanceledException", resultado.Error);
        Assert.Empty(sender.Enviados);
    }

    [Fact]
    public async Task EnviarNuevaSolicitudAsync_SanitizaElAsuntoSinCaracteresDeControl()
    {
        var sender = new FakeEmailSender();
        var service = CrearService(sender, adminNotificationEmail: "admin@facturar.com");
        var solicitud = CrearSolicitud();
        solicitud.RazonSocial = "ACME\r\nBcc: intruso@dominio.com\u0001\u0007";

        await service.EnviarNuevaSolicitudAsync(solicitud, CancellationToken.None);

        var mensaje = Assert.Single(sender.Enviados);
        Assert.DoesNotContain(mensaje.Asunto, char.IsControl);
        Assert.DoesNotContain("\r", mensaje.Asunto);
        Assert.DoesNotContain("\n", mensaje.Asunto);
    }

    [Fact]
    public async Task EnviarSolicitudRechazadaAsync_IncluyeElMotivoEnElMensaje()
    {
        var sender = new FakeEmailSender();
        var service = CrearService(sender);
        var solicitud = CrearSolicitud();
        solicitud.MotivoRechazo = "Datos incompletos";

        await service.EnviarSolicitudRechazadaAsync(solicitud, CancellationToken.None);

        var mensaje = Assert.Single(sender.Enviados);
        Assert.Contains("Datos incompletos", mensaje.CuerpoTexto);
        Assert.Equal("Tu solicitud de acceso fue rechazada", mensaje.Asunto);
    }

    private static EmailService CrearService(
        FakeEmailSender sender,
        string? adminNotificationEmail = null)
        => new(sender, new NotificacionesOptions
        {
            PortalBaseUrl = "http://localhost:5116/",
            AdminNotificationEmail = adminNotificationEmail
        });

    private static SolicitudAltaEmpresa CrearSolicitud() => new()
    {
        RazonSocial = "ACME S.A.",
        Cuit = "20123456786",
        NombreSolicitante = "Ana Pérez",
        EmailSolicitante = "ana@acme.com",
        CreatedAt = new DateTime(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc)
    };

    private sealed class FakeEmailSender : IEmailSender
    {
        public List<EmailMessage> Enviados { get; } = [];

        public Exception? Excepcion { get; init; }

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
