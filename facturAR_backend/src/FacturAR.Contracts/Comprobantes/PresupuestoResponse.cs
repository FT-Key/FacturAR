namespace FacturAR.Contracts.Comprobantes;

public sealed record PresupuestoResponse(
    decimal ImporteNeto,
    decimal ImporteIva,
    decimal ImporteTotal,
    IReadOnlyList<PresupuestoItemResponse> Items);
