namespace FacturAR.Contracts.Comprobantes;

public sealed record UltimoAutorizadoResponse(
    int TipoComprobante,
    int? PuntoVenta,
    long Numero);
