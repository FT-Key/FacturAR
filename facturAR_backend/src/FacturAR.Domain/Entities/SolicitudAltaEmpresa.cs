using FacturAR.Domain.Enums;

namespace FacturAR.Domain.Entities;

public sealed class SolicitudAltaEmpresa
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string RazonSocial { get; set; } = string.Empty;
    public string Cuit { get; set; } = string.Empty;
    public CondicionIva CondicionIva { get; set; } = CondicionIva.ResponsableInscripto;
    public string NombreSolicitante { get; set; } = string.Empty;
    public string EmailSolicitante { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public string Domicilio { get; set; } = string.Empty;
    public string Ciudad { get; set; } = string.Empty;
    public string Provincia { get; set; } = string.Empty;
    public string CodigoPostal { get; set; } = string.Empty;
    public string? Mensaje { get; set; }
    public bool AceptaTerminos { get; set; }
    public DateTime TerminosAceptadosAt { get; set; } = DateTime.UtcNow;
    public EstadoSolicitud Estado { get; set; } = EstadoSolicitud.Pendiente;
    public string? MotivoRechazo { get; set; }
    public Guid? ContribuyenteId { get; set; }
    public Guid? InvitacionId { get; set; }
    public Guid? RevisadaPorAdministradorId { get; set; }
    public DateTime? RevisadaAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
