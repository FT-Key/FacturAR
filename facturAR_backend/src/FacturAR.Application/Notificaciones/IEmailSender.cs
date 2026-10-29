namespace FacturAR.Application.Notificaciones;

public interface IEmailSender
{
    Task<EmailResult> EnviarAsync(EmailMessage mensaje, CancellationToken cancellationToken = default);
}
