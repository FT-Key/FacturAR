using FacturAR.Application.Abstractions;
using FacturAR.Domain.Exceptions;

namespace FacturAR.Application.ApiKeys;

public sealed class RevocarApiKeyService
{
    public const string AccionRevocada = "apikey.revocada";

    private readonly IApiKeyRepository _apiKeys;
    private readonly IEventoAuditoriaRepository _eventos;

    public RevocarApiKeyService(IApiKeyRepository apiKeys, IEventoAuditoriaRepository eventos)
    {
        _apiKeys = apiKeys;
        _eventos = eventos;
    }

    public async Task RevocarAsync(
        Guid id,
        Guid contribuyenteId,
        Guid usuarioId,
        CancellationToken ct = default)
    {
        var apiKey = await _apiKeys.GetByIdAsync(id, contribuyenteId, ct)
            ?? throw new ApiKeyNoEncontradaException();

        if (!apiKey.Activa)
        {
            return;
        }

        var ahora = DateTime.UtcNow;
        apiKey.Activa = false;
        apiKey.RevocadaAt = ahora;

        await _apiKeys.UpdateAsync(apiKey, ct);

        await _eventos.AddAsync(
            ApiKeyEventos.Evento(AccionRevocada, apiKey, usuarioId, ahora),
            ct);
    }
}
