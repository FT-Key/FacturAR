namespace FacturAR.Contracts.Sistema;

public sealed record EstadoSaludResponse(
    string Status,
    string Database,
    DateTime Timestamp,
    string Version,
    string Commit);
