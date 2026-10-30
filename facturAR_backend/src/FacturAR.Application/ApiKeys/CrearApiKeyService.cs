using FacturAR.Application.Abstractions;
using FacturAR.Contracts.ApiKeys;
using FacturAR.Domain.Entities;
using FacturAR.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace FacturAR.Application.ApiKeys;

public sealed class CrearApiKeyService
{
    public const string AccionCreada = "apikey.creada";

    private readonly IApiKeyRepository _apiKeys;
    private readonly IContribuyenteRepository _contribuyentes;
    private readonly IApiKeyGenerator _generator;
    private readonly IApiKeyHasher _hasher;
    private readonly IEventoAuditoriaRepository _eventos;
    private readonly ApiKeysOptions _options;
    private readonly ILogger<CrearApiKeyService> _logger;

    public CrearApiKeyService(
        IApiKeyRepository apiKeys,
        IContribuyenteRepository contribuyentes,
        IApiKeyGenerator generator,
        IApiKeyHasher hasher,
        IEventoAuditoriaRepository eventos,
        ApiKeysOptions options,
        ILogger<CrearApiKeyService> logger)
    {
        _apiKeys = apiKeys;
        _contribuyentes = contribuyentes;
        _generator = generator;
        _hasher = hasher;
        _eventos = eventos;
        _options = options;
        _logger = logger;
    }

    public async Task<ApiKeyCreadaResponse> CrearAsync(
        Guid contribuyenteId,
        Guid usuarioId,
        string? nombre,
        CancellationToken ct = default)
    {
        var nombreNormalizado = NormalizarNombre(nombre);

        var contribuyente = await _contribuyentes.GetByIdAsync(contribuyenteId, ct);
        if (contribuyente is null || !contribuyente.Activo)
        {
            throw new CuentaInactivaException();
        }

        var ahora = DateTime.UtcNow;
        var apiKey = new ApiKey
        {
            ContribuyenteId = contribuyenteId,
            Nombre = nombreNormalizado,
            Activa = true,
            CreatedAt = ahora
        };

        var generado = await InsertarConPrefijoUnicoAsync(apiKey, ct);

        await _eventos.AddAsync(
            ApiKeyEventos.Evento(AccionCreada, apiKey, usuarioId, ahora),
            ct);

        return new ApiKeyCreadaResponse(
            apiKey.Id,
            apiKey.Nombre,
            ApiKeyEventos.PrefijoVisible(apiKey.Prefix, _options),
            generado.Valor,
            apiKey.CreatedAt);
    }

    private async Task<ApiKeyGeneratorValue> InsertarConPrefijoUnicoAsync(ApiKey apiKey, CancellationToken ct)
    {
        var intentos = Math.Max(1, _options.MaxIntentosGeneracionPrefijo);

        for (var intento = 0; intento < intentos; intento++)
        {
            var generado = _generator.Generar();
            apiKey.Prefix = generado.Prefix;
            apiKey.Hash = _hasher.Hash(generado.Valor);

            try
            {
                await _apiKeys.InsertAsync(apiKey, ct);
                return generado;
            }
            catch (ApiKeyPrefijoDuplicadoException)
            {
            }
        }

        _logger.LogError(
            "No se pudo generar un prefijo único de API key tras {Intentos} intentos.",
            intentos);

        throw new InvalidOperationException("No se pudo generar una API key única.");
    }

    private static string NormalizarNombre(string? nombre)
    {
        var normalizado = (nombre ?? string.Empty).Trim();
        if (normalizado.Length is < 1 or > 64 || normalizado.Any(char.IsControl))
        {
            throw new ApiKeyValidationException(
                "El nombre de la API key debe tener entre 1 y 64 caracteres y no puede contener caracteres de control.");
        }

        return normalizado;
    }
}
