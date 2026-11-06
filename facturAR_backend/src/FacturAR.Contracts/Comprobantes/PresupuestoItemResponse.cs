namespace FacturAR.Contracts.Comprobantes;

public sealed record PresupuestoItemResponse(
    string Descripcion,
    decimal Cantidad,
    decimal PrecioUnitario,
    decimal AlicuotaIva,
    decimal Subtotal);
