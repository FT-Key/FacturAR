using FacturAR.Domain.Entities;

namespace FacturAR.Application.Abstractions;

public interface IUsuarioRepository
{
    Task<Usuario?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<Usuario?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task InsertAsync(Usuario usuario, CancellationToken ct = default);
    Task UpdateAsync(Usuario usuario, CancellationToken ct = default);
}