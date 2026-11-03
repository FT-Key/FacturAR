using FacturAR.Application.Comprobantes;
using FacturAR.Domain.Entities;

namespace FacturAR.Application.Abstractions;

public interface IComprobanteRepository
{
    Task<Comprobante?> GetByCaeAsync(string cae, CancellationToken cancellationToken = default);

    Task<Comprobante?> GetByIdAsync(Guid contribuyenteId, Guid id, CancellationToken cancellationToken = default);

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

    Task AddAsync(Comprobante comprobante, CancellationToken cancellationToken = default);

    Task<bool> AjustarSaldoAsync(
        Guid contribuyenteId,
        Guid originalId,
        decimal? saldoEsperado,
        decimal saldoNuevo,
        CancellationToken cancellationToken = default);
}
