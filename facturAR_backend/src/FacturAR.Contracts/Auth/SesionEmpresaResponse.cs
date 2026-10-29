namespace FacturAR.Contracts.Auth;

public sealed record SesionEmpresaResponse(
    Guid UsuarioId,
    string Email,
    string Rol,
    Guid ContribuyenteId,
    string ContribuyenteRazonSocial,
    DateTime ExpiraAtUtc);