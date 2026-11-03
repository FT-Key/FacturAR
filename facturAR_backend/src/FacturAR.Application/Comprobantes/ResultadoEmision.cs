using FacturAR.Contracts.Comprobantes;

namespace FacturAR.Application.Comprobantes;

public sealed record ResultadoEmision(ComprobanteResponse Comprobante, bool EsRepeticion);
