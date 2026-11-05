namespace FacturAR.Contracts.Comprobantes;

public sealed record PeriodoDashboard(DateTime Desde, DateTime Hasta);

public sealed record DashboardResponse(
    PeriodoDashboard Periodo,
    int CantidadComprobantes,
    decimal ImporteTotal,
    string? UltimoCae,
    long UltimoNumeroAutorizado,
    IReadOnlyList<ComprobanteResumenResponse> UltimosComprobantes);
