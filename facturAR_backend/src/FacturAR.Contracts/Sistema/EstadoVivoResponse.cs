namespace FacturAR.Contracts.Sistema;

public sealed record EstadoVivoResponse(
    string Status,
    string Version,
    string Commit);
