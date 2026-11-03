namespace FacturAR.Contracts.Comprobantes;

public sealed record ComprobanteResumenResponse(
    Guid Id,
    int TipoComprobante,
    string TipoComprobanteDescripcion,
    int PuntoVenta,
    long Numero,
    DateTime FechaEmision,
    string Cae,
    string ReceptorRazonSocial,
    string ReceptorDocumentoNumero,
    decimal ImporteTotal,
    string Moneda,
    string Estado);
