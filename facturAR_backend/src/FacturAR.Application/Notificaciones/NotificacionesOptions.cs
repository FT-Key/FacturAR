namespace FacturAR.Application.Notificaciones;

public sealed class NotificacionesOptions
{
    public string PortalBaseUrl { get; set; } = "http://localhost:5116";
    public string? AdminNotificationEmail { get; set; }
}
