using FacturAR.Domain;

namespace FacturAR.Application.Abstractions;

public sealed record ContextoAuditoria(
    string ActorTipo,
    Guid? ContribuyenteId = null,
    Guid? UsuarioId = null,
    Guid? AdministradorId = null)
{
    public static ContextoAuditoria Empresa(Guid contribuyenteId, Guid usuarioId)
        => new(ActorTipos.Empresa, contribuyenteId, usuarioId);

    public static ContextoAuditoria Administrador(Guid administradorId)
        => new(ActorTipos.Administrador, null, null, administradorId);

    public static ContextoAuditoria Sistema(Guid? contribuyenteId = null)
        => new(ActorTipos.Sistema, contribuyenteId);
}
