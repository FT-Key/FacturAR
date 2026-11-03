using FacturAR.Domain.Enums;

namespace FacturAR.Application.Abstractions;

public interface ISecuenciaRepository
{
    Task<long> ObtenerSiguienteNumeroAsync(
        Guid contribuyenteId,
        int puntoVenta,
        TipoComprobante tipo,
        CancellationToken ct = default);

    Task<long> ObtenerUltimoNumeroAsync(
        Guid contribuyenteId,
        int puntoVenta,
        TipoComprobante tipo,
        CancellationToken ct = default);

    Task<long> ObtenerMaximoUltimoNumeroAsync(
        Guid contribuyenteId,
        TipoComprobante tipo,
        CancellationToken ct = default);
}
