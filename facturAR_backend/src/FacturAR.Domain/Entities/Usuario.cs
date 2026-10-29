using FacturAR.Domain.Enums;

namespace FacturAR.Domain.Entities;

public sealed class Usuario
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ContribuyenteId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public RolUsuario Rol { get; set; } = RolUsuario.Emisor;
    public bool Activo { get; set; } = true;
    public DateTime? EmailVerificadoAt { get; set; }
    public DateTime? UltimoAcceso { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
