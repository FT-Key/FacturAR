using FacturAR.Domain.Enums;

namespace FacturAR.Application.Comprobantes;

public sealed record FiltroListadoComprobantes(
    DateTime? Desde,
    DateTime? HastaExclusivo,
    TipoComprobante? Tipo,
    EstadoComprobante? Estado,
    int? PuntoVenta,
    string? ReceptorDocumentoNumero);
