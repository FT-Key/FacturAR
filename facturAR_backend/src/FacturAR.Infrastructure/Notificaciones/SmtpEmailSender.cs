using System.Diagnostics;
using System.Net;
using System.Net.Mail;
using System.Text;
using FacturAR.Application.Notificaciones;
using Microsoft.Extensions.Logging;

namespace FacturAR.Infrastructure.Notificaciones;

public sealed class SmtpEmailSender(EmailOptions options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task<EmailResult> EnviarAsync(
        EmailMessage mensaje,
        CancellationToken cancellationToken = default)
    {
        var inicio = Stopwatch.GetTimestamp();
        var timeout = TimeSpan.FromSeconds(options.TimeoutSegundos >= 0 ? options.TimeoutSegundos : 15);

        try
        {
            using var cliente = CrearCliente(timeout);
            using var correo = CrearCorreo(mensaje);

            await cliente.SendMailAsync(correo).WaitAsync(timeout, cancellationToken).ConfigureAwait(false);

            var duracionMs = (long)Stopwatch.GetElapsedTime(inicio).TotalMilliseconds;
            logger.LogInformation(
                "Email SMTP enviado. plantilla={Plantilla} exito={Exito} duracionMs={DuracionMs} destinatario={Destinatario}",
                mensaje.Plantilla,
                true,
                duracionMs,
                EmailMasker.Enmascarar(mensaje.Para));

            return EmailResult.Ok(DateTime.UtcNow);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TimeoutException)
        {
            return RegistrarFallo(
                mensaje,
                inicio,
                "SMTP_TIMEOUT: el envío superó el tiempo máximo configurado.");
        }
        catch (Exception ex)
        {
            return RegistrarFallo(
                mensaje,
                inicio,
                $"Error de envío SMTP ({ex.GetType().Name}).");
        }
    }

    private EmailResult RegistrarFallo(EmailMessage mensaje, long inicio, string error)
    {
        var duracionMs = (long)Stopwatch.GetElapsedTime(inicio).TotalMilliseconds;
        logger.LogWarning(
            "Email SMTP no enviado. plantilla={Plantilla} exito={Exito} duracionMs={DuracionMs} destinatario={Destinatario} error={Error}",
            mensaje.Plantilla,
            false,
            duracionMs,
            EmailMasker.Enmascarar(mensaje.Para),
            error);

        return EmailResult.Fallido(error);
    }

    private SmtpClient CrearCliente(TimeSpan timeout)
    {
        var timeoutInterno = (int)Math.Min(int.MaxValue, timeout.TotalMilliseconds + 5000);
        var cliente = new SmtpClient
        {
            Host = options.Host ?? string.Empty,
            Port = options.Port,
            EnableSsl = options.UseSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Timeout = timeoutInterno
        };

        if (!string.IsNullOrWhiteSpace(options.User))
        {
            cliente.Credentials = new NetworkCredential(options.User, options.Password);
        }

        return cliente;
    }

    private MailMessage CrearCorreo(EmailMessage mensaje)
    {
        var correo = new MailMessage
        {
            From = new MailAddress(options.From, options.FromName, Encoding.UTF8),
            Subject = mensaje.Asunto,
            SubjectEncoding = Encoding.UTF8,
            Body = mensaje.CuerpoTexto,
            BodyEncoding = Encoding.UTF8,
            IsBodyHtml = false
        };

        correo.To.Add(mensaje.Para);

        if (!string.IsNullOrWhiteSpace(mensaje.CuerpoHtml))
        {
            correo.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(
                mensaje.CuerpoHtml,
                Encoding.UTF8,
                "text/html"));
        }

        return correo;
    }
}
