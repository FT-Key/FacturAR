using FacturAR.Domain.Entities;

namespace FacturAR.Application.Abstractions;

public interface IContribuyenteRepository
{
    Task<Contribuyente?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<bool> ExistePorCuitAsync(string cuit, CancellationToken ct = default);

    Task InsertarAsync(Contribuyente contribuyente, CancellationToken ct = default);
}
