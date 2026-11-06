using FacturAR.Domain.Entities;

namespace FacturAR.Application.Abstractions;

public interface IApiKeyRepository
{
    Task<ApiKey?> GetByIdAsync(Guid id, Guid contribuyenteId, CancellationToken ct = default);

    Task<IReadOnlyList<ApiKey>> ListByContribuyenteAsync(Guid contribuyenteId, CancellationToken ct = default);

    Task<ApiKey?> GetByPrefixAsync(string prefix, CancellationToken ct = default);

    Task InsertAsync(ApiKey apiKey, CancellationToken ct = default);

    Task UpdateAsync(ApiKey apiKey, CancellationToken ct = default);

    Task<bool> ActualizarUltimoUsoAsync(
        Guid id,
        Guid contribuyenteId,
        DateTime ahoraUtc,
        TimeSpan intervalo,
        CancellationToken ct = default);
}
