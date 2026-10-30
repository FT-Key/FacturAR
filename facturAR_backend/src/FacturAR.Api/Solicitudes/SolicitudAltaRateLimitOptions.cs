namespace FacturAR.Api.Solicitudes;

public sealed class SolicitudAltaRateLimitOptions
{
    public const string SectionName = "RateLimit:SolicitudAlta";

    public int PorIp { get; set; } = 5;
    public int PorEmail { get; set; } = 3;
    public bool ConfiarEnProxy { get; set; }
}
