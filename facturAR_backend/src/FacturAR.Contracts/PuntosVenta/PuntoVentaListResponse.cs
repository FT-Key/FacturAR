namespace FacturAR.Contracts.PuntosVenta;

public sealed record PuntoVentaListResponse(IReadOnlyList<PuntoVentaResumenResponse> Items);
