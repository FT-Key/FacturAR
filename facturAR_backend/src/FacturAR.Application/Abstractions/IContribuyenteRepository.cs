using FacturAR.Domain.Entities;

namespace FacturAR.Application.Abstractions;

public interface IContribuyenteRepository
{
    Task<Contribuyente?> GetByIdAsync(Guid id, CancellationToken ct = default);
}