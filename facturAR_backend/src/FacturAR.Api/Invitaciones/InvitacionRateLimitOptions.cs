namespace FacturAR.Api.Invitaciones;

public sealed class InvitacionRateLimitOptions
{
    public const string SectionName = "RateLimit:Invitacion";

    public int ValidacionesPorIp { get; set; } = 20;
    public int ActivacionesPorIp { get; set; } = 10;
}
