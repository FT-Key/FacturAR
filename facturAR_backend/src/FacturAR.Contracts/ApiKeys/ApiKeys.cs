namespace FacturAR.Contracts.ApiKeys;

public sealed record CrearApiKeyRequest(string Nombre);

public sealed record ApiKeyCreadaResponse(
    Guid Id,
    string Nombre,
    string Prefix,
    string Valor,
    DateTime CreatedAt);

public sealed record ApiKeyRegeneradaResponse(
    Guid Id,
    string Nombre,
    string Prefix,
    string Valor,
    DateTime RegeneradaAt);

public sealed record ApiKeyResumenResponse(
    Guid Id,
    string Nombre,
    string Prefix,
    bool Activa,
    DateTime CreatedAt,
    DateTime? UltimoUso,
    DateTime? RevocadaAt,
    DateTime? RegeneradaAt);

public sealed record ApiKeyListResponse(IReadOnlyList<ApiKeyResumenResponse> Items);
