namespace FacturAR.Contracts.Comunes;

public sealed record PaginaResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    long Total,
    int TotalPages);
