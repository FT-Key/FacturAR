using FacturAR.Domain.Enums;

namespace FacturAR.Domain.Entities;

public sealed class Invitacion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ContribuyenteId { get; set; }
    public Guid? SolicitudId { get; set; }
    public string Email { get; set; } = string.Empty;
    public RolUsuario Rol { get; set; } = RolUsuario.Administrador;
    public string TokenHash { get; set; } = string.Empty;
    public string TokenPrefix { get; set; } = string.Empty;
    public EstadoInvitacion Estado { get; set; } = EstadoInvitacion.Pendiente;
    public DateTime ExpiraAt { get; set; }
    public DateTime? UsadaAt { get; set; }
    public DateTime? RevocadaAt { get; set; }
    public Guid CreadaPorAdministradorId { get; set; }
    public DateTime? UltimoEnvioAt { get; set; }
    public int IntentosEnvio { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public EstadoInvitacion EstadoEfectivo(DateTime ahora)
        => Estado == EstadoInvitacion.Pendiente && ExpiraAt <= ahora
            ? EstadoInvitacion.Vencida
            : Estado;
}
