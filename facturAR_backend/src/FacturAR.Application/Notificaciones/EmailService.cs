using System.Net.Mail;
using System.Text;
using FacturAR.Domain.Entities;

namespace FacturAR.Application.Notificaciones;

public sealed class EmailService(IEmailSender emailSender, NotificacionesOptions options)
{
    private const string ErrorDestinatario = "El destinatario no tiene un formato de email válido.";
    private const string ErrorAdminNoConfigurada = "ADMIN_NOTIFICATION_EMAIL_NO_CONFIGURADA";

    public bool NotificacionAdministracionConfigurada
        => !string.IsNullOrWhiteSpace(options.AdminNotificationEmail);

    public Task<EmailResult> EnviarSolicitudRecibidaAsync(
        SolicitudAltaEmpresa solicitud,
        CancellationToken ct)
        => EnviarAsync(
            solicitud.EmailSolicitante,
            PlantillasEmail.CrearSolicitudRecibida(solicitud),
            ct);

    public Task<EmailResult> EnviarNuevaSolicitudAsync(
        SolicitudAltaEmpresa solicitud,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(options.AdminNotificationEmail))
        {
            return Task.FromResult(EmailResult.Fallido(ErrorAdminNoConfigurada));
        }

        return EnviarAsync(
            options.AdminNotificationEmail,
            PlantillasEmail.CrearNuevaSolicitud(solicitud, options.PortalBaseUrl),
            ct);
    }

    public Task<EmailResult> EnviarSolicitudAprobadaAsync(
        SolicitudAltaEmpresa solicitud,
        string token,
        DateTime expiraAt,
        CancellationToken ct)
        => EnviarAsync(
            solicitud.EmailSolicitante,
            PlantillasEmail.CrearSolicitudAprobada(solicitud, CrearLinkActivacion(token), expiraAt),
            ct);

    public Task<EmailResult> EnviarSolicitudRechazadaAsync(
        SolicitudAltaEmpresa solicitud,
        CancellationToken ct)
        => EnviarAsync(
            solicitud.EmailSolicitante,
            PlantillasEmail.CrearSolicitudRechazada(solicitud),
            ct);

    public Task<EmailResult> EnviarInvitacionReenviadaAsync(
        SolicitudAltaEmpresa solicitud,
        string token,
        DateTime expiraAt,
        CancellationToken ct)
        => EnviarAsync(
            solicitud.EmailSolicitante,
            PlantillasEmail.CrearInvitacionReenviada(solicitud, CrearLinkActivacion(token), expiraAt),
            ct);

    private async Task<EmailResult> EnviarAsync(
        string destinatario,
        ContenidoEmail contenido,
        CancellationToken ct)
    {
        if (!EsDestinatarioValido(destinatario))
        {
            return EmailResult.Fallido(ErrorDestinatario);
        }

        var mensaje = new EmailMessage(
            destinatario.Trim(),
            SanitizarAsunto(contenido.Asunto),
            contenido.Html,
            contenido.Texto,
            contenido.Plantilla);

        try
        {
            return await emailSender.EnviarAsync(mensaje, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return EmailResult.Fallido($"Error de envío ({ex.GetType().Name}).");
        }
    }

    private string CrearLinkActivacion(string token)
        => $"{options.PortalBaseUrl.TrimEnd('/')}/activar?token={Uri.EscapeDataString(token)}";

    private static bool EsDestinatarioValido(string? destinatario)
    {
        if (string.IsNullOrWhiteSpace(destinatario))
        {
            return false;
        }

        var normalizado = destinatario.Trim();
        return MailAddress.TryCreate(normalizado, out var direccion)
            && direccion is not null
            && string.Equals(direccion.Address, normalizado, StringComparison.OrdinalIgnoreCase);
    }

    private static string SanitizarAsunto(string asunto)
    {
        var sanitizado = new StringBuilder(asunto.Length);
        foreach (var caracter in asunto)
        {
            sanitizado.Append(char.IsControl(caracter) ? ' ' : caracter);
        }

        return sanitizado.ToString().Trim();
    }
}
