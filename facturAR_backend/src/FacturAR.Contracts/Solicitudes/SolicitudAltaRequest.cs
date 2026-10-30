namespace FacturAR.Contracts.Solicitudes;

public sealed record SolicitudAltaRequest(
    string RazonSocial,
    string Cuit,
    int CondicionIva,
    string NombreSolicitante,
    string Email,
    string Telefono,
    string Domicilio,
    string Ciudad,
    string Provincia,
    string CodigoPostal,
    string? Mensaje,
    bool AceptaTerminos);
