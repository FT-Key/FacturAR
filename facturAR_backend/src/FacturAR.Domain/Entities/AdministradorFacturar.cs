using FacturAR.Domain.Enums;

namespace FacturAR.Domain.Entities;

public sealed class AdministradorFacturar
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Nombre { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public RolGlobal Rol { get; set; } = RolGlobal.Soporte;
    public bool Activo { get; set; } = true;
    public DateTime? UltimoAcceso { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}