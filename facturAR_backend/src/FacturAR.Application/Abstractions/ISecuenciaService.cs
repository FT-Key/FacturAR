using FacturAR.Domain.Enums;

namespace FacturAR.Application.Abstractions;

public interface ISecuenciaService
{
    Task<long> ObtenerSiguienteNumeroAsync(
        Guid contribuyenteId,
        int puntoVenta,
        TipoComprobante tipo,
        CancellationToken ct = default);
}
