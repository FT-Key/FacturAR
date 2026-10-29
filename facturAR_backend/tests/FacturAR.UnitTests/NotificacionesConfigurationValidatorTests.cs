using FacturAR.Application.Notificaciones;
using FacturAR.Infrastructure.Notificaciones;

namespace FacturAR.UnitTests;

public sealed class NotificacionesConfigurationValidatorTests
{
    [Fact]
    public void Validar_ConProviderInvalido_LanzaInvalidOperationException()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            NotificacionesConfigurationValidator.Validar(
                new EmailOptions { Provider = "SendGrid" },
                OpcionesValidas(),
                esDevelopment: true));

        Assert.Contains("Email:Provider", ex.Message);
    }

    [Fact]
    public void Validar_ConSmtpSinHost_LanzaInvalidOperationException()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            NotificacionesConfigurationValidator.Validar(
                new EmailOptions { Provider = "Smtp", Host = "" },
                OpcionesValidas(),
                esDevelopment: false));

        Assert.Contains("Email:Host", ex.Message);
    }

    [Fact]
    public void Validar_ConPortalBaseUrlRelativa_LanzaInvalidOperationException()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            NotificacionesConfigurationValidator.Validar(
                new EmailOptions { Provider = "Smtp", Host = "smtp.dominio.com" },
                new NotificacionesOptions { PortalBaseUrl = "/portal" },
                esDevelopment: false));

        Assert.Contains("Portal:BaseUrl", ex.Message);
    }

    [Fact]
    public void Validar_ConAdminNotificationEmailInvalida_LanzaInvalidOperationException()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            NotificacionesConfigurationValidator.Validar(
                new EmailOptions { Provider = "Smtp", Host = "smtp.dominio.com" },
                new NotificacionesOptions
                {
                    PortalBaseUrl = "https://portal.dominio.com",
                    AdminNotificationEmail = "no-es-email"
                },
                esDevelopment: false));

        Assert.Contains("Admin:NotificationEmail", ex.Message);
    }

    [Fact]
    public void Validar_ConFileFueraDeDevelopment_LanzaInvalidOperationException()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            NotificacionesConfigurationValidator.Validar(
                new EmailOptions { Provider = "File" },
                OpcionesValidas(),
                esDevelopment: false));

        Assert.Contains("Development", ex.Message);
    }

    [Fact]
    public void Validar_ConFileEnDevelopment_NoLanza()
    {
        NotificacionesConfigurationValidator.Validar(
            new EmailOptions { Provider = "File" },
            OpcionesValidas(),
            esDevelopment: true);
    }

    [Fact]
    public void Validar_ConSmtpCompleto_NoLanza()
    {
        NotificacionesConfigurationValidator.Validar(
            new EmailOptions
            {
                Provider = "Smtp",
                Host = "smtp.dominio.com",
                Port = 587,
                User = "usuario",
                Password = "secreto-smtp",
                UseSsl = true
            },
            new NotificacionesOptions
            {
                PortalBaseUrl = "https://portal.dominio.com",
                AdminNotificationEmail = "admin@dominio.com"
            },
            esDevelopment: false);
    }

    [Fact]
    public void Validar_ConSmtpSinAdminNotificationEmail_NoLanza()
    {
        NotificacionesConfigurationValidator.Validar(
            new EmailOptions { Provider = "Smtp", Host = "smtp.dominio.com" },
            new NotificacionesOptions
            {
                PortalBaseUrl = "https://portal.dominio.com",
                AdminNotificationEmail = null
            },
            esDevelopment: false);
    }

    [Fact]
    public void Validar_ConProviderEnMinusculas_NoLanza()
    {
        NotificacionesConfigurationValidator.Validar(
            new EmailOptions { Provider = "smtp", Host = "smtp.dominio.com" },
            OpcionesValidas(),
            esDevelopment: false);
    }

    private static NotificacionesOptions OpcionesValidas() => new()
    {
        PortalBaseUrl = "https://portal.dominio.com",
        AdminNotificationEmail = "admin@dominio.com"
    };
}
