namespace FacturAR.Contracts.Comprobantes;

public sealed record ComprobanteDetalleResponse(
    Guid Id,
    int TipoComprobante,
    string TipoComprobanteDescripcion,
    int PuntoVenta,
    long Numero,
    DateTime FechaEmision,
    string Cae,
    DateTime CaeVencimiento,
    string EmisorCuit,
    string EmisorRazonSocial,
    ReceptorComprobanteResponse Receptor,
    decimal ImporteNeto,
    decimal ImporteIva,
    decimal ImporteTotal,
    string Moneda,
    string Estado,
    string? Observaciones,
    IReadOnlyList<ComprobanteItemResponse> Items);

public sealed record ComprobanteItemResponse(
    string Descripcion,
    decimal Cantidad,
    decimal PrecioUnitario,
    decimal AlicuotaIva,
    decimal Subtotal);
