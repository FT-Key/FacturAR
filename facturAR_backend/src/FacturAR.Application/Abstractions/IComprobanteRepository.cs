using FacturAR.Domain.Entities;

namespace FacturAR.Application.Abstractions;

public interface IComprobanteRepository
{
    Task<Comprobante?> GetByCaeAsync(string cae, CancellationToken cancellationToken = default);
    Task<Comprobante?> GetByIdAsync(Guid contribuyenteId, Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(Comprobante comprobante, CancellationToken cancellationToken = default);

    Task<bool> AjustarSaldoAsync(
        Guid contribuyenteId,
        Guid originalId,
        decimal? saldoEsperado,
        decimal saldoNuevo,
        CancellationToken cancellationToken = default);
}
