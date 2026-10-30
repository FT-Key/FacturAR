using FacturAR.Application.Abstractions;
using FacturAR.Contracts.ApiKeys;

namespace FacturAR.Application.ApiKeys;

public sealed class ListarApiKeysService
{
    private readonly IApiKeyRepository _apiKeys;
    private readonly ApiKeysOptions _options;

    public ListarApiKeysService(IApiKeyRepository apiKeys, ApiKeysOptions options)
    {
        _apiKeys = apiKeys;
        _options = options;
    }

    public async Task<ApiKeyListResponse> ListarAsync(Guid contribuyenteId, CancellationToken ct = default)
    {
        var claves = await _apiKeys.ListByContribuyenteAsync(contribuyenteId, ct);

        var items = claves
            .Select(apiKey => new ApiKeyResumenResponse(
                apiKey.Id,
                apiKey.Nombre,
                ApiKeyEventos.PrefijoVisible(apiKey.Prefix, _options),
                apiKey.Activa,
                apiKey.CreatedAt,
                apiKey.UltimoUso,
                apiKey.RevocadaAt,
                apiKey.RegeneradaAt))
            .ToList();

        return new ApiKeyListResponse(items);
    }
}
