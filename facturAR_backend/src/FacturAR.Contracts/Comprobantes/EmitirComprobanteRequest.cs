namespace FacturAR.Contracts.Comprobantes;

public sealed record EmitirComprobanteRequest(
    int TipoComprobante,
    int PuntoVenta,
    DateTime? FechaEmision,
    ReceptorComprobanteRequest? Receptor,
    List<ItemComprobanteRequest>? Items,
    decimal? ImporteTotal,
    string? Observaciones,
    Guid? ComprobanteOriginalId = null);

public sealed record ReceptorComprobanteRequest(
    string? DocumentoTipo,
    string? DocumentoNumero,
    string? RazonSocial,
    int? CondicionIva);

public sealed record ItemComprobanteRequest(
    string? Descripcion,
    decimal Cantidad,
    decimal PrecioUnitario,
    decimal AlicuotaIva);
