using FacturAR.Application.Comprobantes;
using FacturAR.Domain.Entities;

namespace FacturAR.Application.Abstractions;

public interface IComprobanteRepository
{
    Task<Comprobante?> GetByCaeAsync(string cae, CancellationToken cancellationToken = default);
    Task<Comprobante?> GetByIdAsync(Guid contribuyenteId, Guid id, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Comprobante> Items, long Total)> ListarAsync(
        Guid contribuyenteId,
        FiltroListadoComprobantes filtro,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task AddAsync(Comprobante comprobante, CancellationToken cancellationToken = default);
}
