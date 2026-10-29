using System.Net;
using System.Net.Sockets;
using FacturAR.Application.Notificaciones;
using FacturAR.Domain.Entities;
using FacturAR.Infrastructure.Notificaciones;

namespace FacturAR.IntegrationTests;

public sealed class SmtpEmailSenderTests
{
    [Fact]
    public async Task EnviarAsync_ConHostInexistente_DevuelveFallidoSinPropagar()
    {
        const string token = "token-super-secreto-123";
        var logger = new RecordingLogger<SmtpEmailSender>();
        var sender = CrearSender("smtp.invalid", 587, 5, logger);

        var resultado = await sender.EnviarAsync(CrearMensaje(token));

        Assert.False(resultado.Exitoso);
        Assert.NotNull(resultado.Error);
        Assert.StartsWith("Error de envío SMTP", resultado.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("SMTP_TIMEOUT", resultado.Error, StringComparison.Ordinal);
        Assert.DoesNotContain(token, resultado.Error);
        Assert.All(logger.Messages, m => Assert.DoesNotContain(token, m));
    }

    [Fact]
    public async Task EnviarAsync_CuandoElServidorAceptaYNoResponde_DevuelveSmtpTimeout()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var puerto = ((IPEndPoint)listener.LocalEndpoint).Port;
        TcpClient? aceptado = null;

        try
        {
            var aceptar = Task.Run(async () =>
            {
                try
                {
                    aceptado = await listener.AcceptTcpClientAsync();
                }
                catch (Exception)
                {
                }
            });

            var sender = CrearSender("127.0.0.1", puerto, 1, new RecordingLogger<SmtpEmailSender>());

            var resultado = await sender.EnviarAsync(CrearMensaje("token-super-secreto-123"));

            Assert.False(resultado.Exitoso);
            Assert.NotNull(resultado.Error);
            Assert.StartsWith("SMTP_TIMEOUT", resultado.Error, StringComparison.Ordinal);

            await aceptar;
        }
        finally
        {
            listener.Stop();
            aceptado?.Dispose();
        }
    }

    [Fact]
    public async Task EmailService_ConSmtpInaccesible_DevuelveFallidoSinTokenEnElError()
    {
        const string token = "token-super-secreto-123";
        var sender = CrearSender("smtp.invalid", 587, 5, new RecordingLogger<SmtpEmailSender>());
        var service = new EmailService(
            sender,
            new NotificacionesOptions { PortalBaseUrl = "http://localhost:5116" });

        var resultado = await service.EnviarSolicitudAprobadaAsync(
            CrearSolicitud(),
            token,
            DateTime.UtcNow.AddHours(48),
            CancellationToken.None);

        Assert.False(resultado.Exitoso);
        Assert.Null(resultado.EnviadoAt);
        Assert.NotNull(resultado.Error);
        Assert.StartsWith("Error de envío SMTP", resultado.Error, StringComparison.Ordinal);
        Assert.DoesNotContain(token, resultado.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("activar?token", resultado.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("ACME", resultado.Error, StringComparison.Ordinal);
    }

    private static SolicitudAltaEmpresa CrearSolicitud() => new()
    {
        RazonSocial = "ACME S.A.",
        Cuit = "20123456786",
        NombreSolicitante = "Ana Pérez",
        EmailSolicitante = "ana@acme.com",
        CreatedAt = new DateTime(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc)
    };

    private static SmtpEmailSender CrearSender(
        string host,
        int puerto,
        int timeoutSegundos,
        RecordingLogger<SmtpEmailSender> logger)
        => new(
            new EmailOptions
            {
                Host = host,
                Port = puerto,
                From = "facturar@localhost",
                FromName = "FacturAR",
                UseSsl = false,
                TimeoutSegundos = timeoutSegundos
            },
            logger);

    private static EmailMessage CrearMensaje(string token)
        => new(
            "destino@dominio.com",
            "Asunto",
            "<html lang=\"es\"><body>cuerpo</body></html>",
            $"link http://localhost:5116/activar?token={token}",
            PlantillasEmail.NombreSolicitudAprobada);
}
