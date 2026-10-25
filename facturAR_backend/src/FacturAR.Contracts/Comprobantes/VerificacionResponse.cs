namespace FacturAR.Contracts.Comprobantes;

public sealed record VerificacionResponse(
    string Cae,
    string CuitEmisor,
    string RazonSocialEmisor,
    int TipoComprobante,
    string TipoComprobanteDescripcion,
    int PuntoVenta,
    long Numero,
    DateTime FechaEmision,
    decimal ImporteTotal,
    string Moneda,
    string Estado,
    DateTime CaeVencimiento);
