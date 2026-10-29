using FacturAR.Domain.Entities;

namespace FacturAR.Application.Abstractions;

public interface IAdministradorRepository
{
    Task<AdministradorFacturar?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<AdministradorFacturar?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default);
    Task InsertAsync(AdministradorFacturar administrador, CancellationToken ct = default);
    Task UpdateAsync(AdministradorFacturar administrador, CancellationToken ct = default);
}