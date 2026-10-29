namespace FacturAR.Application.Abstractions;

public interface ITenantContext
{
    Guid? ContribuyenteId { get; }
    Guid? UsuarioId { get; }
    Guid? AdministradorId { get; }
}