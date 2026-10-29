namespace FacturAR.Contracts.Auth;

public sealed record SesionAdminResponse(
    Guid AdministradorId,
    string Email,
    string Nombre,
    string RolGlobal,
    DateTime ExpiraAtUtc);