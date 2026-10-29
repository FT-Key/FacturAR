namespace FacturAR.Infrastructure.Notificaciones;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string Provider { get; set; } = string.Empty;
    public string From { get; set; } = "facturar@localhost";
    public string FromName { get; set; } = "FacturAR";
    public string? Host { get; set; }
    public int Port { get; set; } = 587;
    public string? User { get; set; }
    public string? Password { get; set; }
    public bool UseSsl { get; set; } = true;
    public string DirectorioSalida { get; set; } = "emails";
    public int TimeoutSegundos { get; set; } = 15;
}
