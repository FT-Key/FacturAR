using FacturAR.Domain.Enums;

namespace FacturAR.Domain.Entities;

public sealed class Contribuyente
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Cuit { get; set; } = string.Empty;
    public string RazonSocial { get; set; } = string.Empty;
    public CondicionIva CondicionIva { get; set; } = CondicionIva.ResponsableInscripto;
    public string? Email { get; set; }
    public bool Activo { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
