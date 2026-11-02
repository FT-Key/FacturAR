namespace FacturAR.Contracts.Comprobantes;

public sealed record ComprobanteResponse(
    Guid Id,
    int TipoComprobante,
    string TipoComprobanteDescripcion,
    int PuntoVenta,
    long Numero,
    DateTime FechaEmision,
    string Cae,
    DateTime CaeVencimiento,
    ReceptorComprobanteResponse Receptor,
    decimal ImporteNeto,
    decimal ImporteIva,
    decimal ImporteTotal,
    string Moneda,
    string Estado);

public sealed record ReceptorComprobanteResponse(
    string DocumentoTipo,
    string DocumentoNumero,
    string RazonSocial,
    int CondicionIva);
