using FacturAR.Domain.Entities;

namespace FacturAR.Application.Abstractions;

public interface IComprobanteRepository
{
    Task<Comprobante?> GetByCaeAsync(string cae, CancellationToken cancellationToken = default);
    Task AddAsync(Comprobante comprobante, CancellationToken cancellationToken = default);
}
