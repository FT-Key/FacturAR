namespace FacturAR.Domain.Entities;

public sealed class EventoAuditoria
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? ContribuyenteId { get; set; }
    public Guid? UsuarioId { get; set; }
    public Guid? AdministradorId { get; set; }
    public string Accion { get; set; } = string.Empty;
    public string Entidad { get; set; } = string.Empty;
    public string? EntidadId { get; set; }
    public string ActorTipo { get; set; } = string.Empty;
    public string? Datos { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
