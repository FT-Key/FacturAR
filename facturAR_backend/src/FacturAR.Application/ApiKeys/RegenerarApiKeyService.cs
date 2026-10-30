using FacturAR.Application.Abstractions;
using FacturAR.Contracts.ApiKeys;
using FacturAR.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace FacturAR.Application.ApiKeys;

public sealed class RegenerarApiKeyService
{
    public const string AccionRegenerada = "apikey.regenerada";

    private readonly IApiKeyRepository _apiKeys;
    private readonly IApiKeyGenerator _generator;
    private readonly IApiKeyHasher _hasher;
    private readonly IEventoAuditoriaRepository _eventos;
    private readonly ApiKeysOptions _options;
    private readonly ILogger<RegenerarApiKeyService> _logger;

    public RegenerarApiKeyService(
        IApiKeyRepository apiKeys,
        IApiKeyGenerator generator,
        IApiKeyHasher hasher,
        IEventoAuditoriaRepository eventos,
        ApiKeysOptions options,
        ILogger<RegenerarApiKeyService> logger)
    {
        _apiKeys = apiKeys;
        _generator = generator;
        _hasher = hasher;
        _eventos = eventos;
        _options = options;
        _logger = logger;
    }

    public async Task<ApiKeyRegeneradaResponse> RegenerarAsync(
        Guid id,
        Guid contribuyenteId,
        Guid usuarioId,
        CancellationToken ct = default)
    {
        var apiKey = await _apiKeys.GetByIdAsync(id, contribuyenteId, ct)
            ?? throw new ApiKeyNoEncontradaException();

        var ahora = DateTime.UtcNow;
        var generado = await ActualizarConPrefijoUnicoAsync(apiKey, ahora, ct);

        await _eventos.AddAsync(
            ApiKeyEventos.Evento(AccionRegenerada, apiKey, usuarioId, ahora),
            ct);

        return new ApiKeyRegeneradaResponse(
            apiKey.Id,
            apiKey.Nombre,
            ApiKeyEventos.PrefijoVisible(apiKey.Prefix, _options),
            generado.Valor,
            ahora);
    }

    private async Task<ApiKeyGeneratorValue> ActualizarConPrefijoUnicoAsync(
        FacturAR.Domain.Entities.ApiKey apiKey,
        DateTime ahora,
        CancellationToken ct)
    {
        var intentos = Math.Max(1, _options.MaxIntentosGeneracionPrefijo);

        for (var intento = 0; intento < intentos; intento++)
        {
            var generado = _generator.Generar();
            apiKey.Prefix = generado.Prefix;
            apiKey.Hash = _hasher.Hash(generado.Valor);
            apiKey.Activa = true;
            apiKey.RevocadaAt = null;
            apiKey.RegeneradaAt = ahora;

            try
            {
                await _apiKeys.UpdateAsync(apiKey, ct);
                return generado;
            }
            catch (ApiKeyPrefijoDuplicadoException)
            {
            }
        }

        _logger.LogError(
            "No se pudo regenerar la API key {ApiKeyId} con un prefijo único tras {Intentos} intentos.",
            apiKey.Id,
            intentos);

        throw new InvalidOperationException("No se pudo regenerar la API key.");
    }
}
