using System.Net.Mail;
using FacturAR.Application.Notificaciones;

namespace FacturAR.Infrastructure.Notificaciones;

public static class NotificacionesConfigurationValidator
{
    public static void Validar(
        EmailOptions email,
        NotificacionesOptions notificaciones,
        bool esDevelopment)
    {
        if (!EsProviderValido(email.Provider))
        {
            throw new InvalidOperationException("Email:Provider debe ser File o Smtp.");
        }

        if (EsSmtp(email.Provider) && string.IsNullOrWhiteSpace(email.Host))
        {
            throw new InvalidOperationException("Email:Host es obligatorio cuando Email:Provider es Smtp.");
        }

        if (!Uri.TryCreate(notificaciones.PortalBaseUrl, UriKind.Absolute, out var portalBaseUrl)
            || (portalBaseUrl.Scheme != Uri.UriSchemeHttp && portalBaseUrl.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException("Portal:BaseUrl debe ser una URL absoluta con esquema http o https.");
        }

        if (!string.IsNullOrWhiteSpace(notificaciones.AdminNotificationEmail)
            && !MailAddress.TryCreate(notificaciones.AdminNotificationEmail, out _))
        {
            throw new InvalidOperationException("Admin:NotificationEmail debe tener un formato de email válido.");
        }

        if (EsFile(email.Provider) && !esDevelopment)
        {
            throw new InvalidOperationException("Email:Provider=File solo está permitido en Development.");
        }
    }

    private static bool EsProviderValido(string provider)
        => EsFile(provider) || EsSmtp(provider);

    private static bool EsFile(string provider)
        => string.Equals(provider, "File", StringComparison.OrdinalIgnoreCase);

    private static bool EsSmtp(string provider)
        => string.Equals(provider, "Smtp", StringComparison.OrdinalIgnoreCase);
}
