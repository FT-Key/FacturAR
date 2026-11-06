using FacturAR.Application.Comprobantes;
using FacturAR.Domain.Entities;
using FacturAR.Domain.Enums;

namespace FacturAR.Application.Abstractions;

public interface IComprobanteRepository
{
    Task<Comprobante?> GetByCaeAsync(string cae, CancellationToken cancellationToken = default);

    Task<Comprobante?> GetByIdAsync(Guid contribuyenteId, Guid id, CancellationToken cancellationToken = default);

    Task<Comprobante?> GetByNumeracionAsync(
        Guid contribuyenteId,
        int puntoVenta,
        TipoComprobante tipo,
        long numero,
        CancellationToken cancellationToken = default);

    Task<Comprobante?> GetUltimoEmitidoAsync(
        Guid contribuyenteId,
        int puntoVenta,
        TipoComprobante tipo,
        CancellationToken cancellationToken = default);

    Task<Comprobante?> GetByIdempotencyKeyAsync(
        Guid contribuyenteId,
        string clave,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<Comprobante> Items, long Total)> ListarAsync(
        Guid contribuyenteId,
        FiltroListadoComprobantes filtro,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<MetricasDashboard> ObtenerMetricasAsync(
        Guid contribuyenteId,
        DateTime desde,
        DateTime hastaExclusivo,
        CancellationToken cancellationToken = default);

    Task AddAsync(Comprobante comprobante, CancellationToken cancellationToken = default);

    Task<bool> AjustarSaldoAsync(
        Guid contribuyenteId,
        Guid originalId,
        decimal? saldoEsperado,
        decimal saldoNuevo,
        CancellationToken cancellationToken = default);
}
