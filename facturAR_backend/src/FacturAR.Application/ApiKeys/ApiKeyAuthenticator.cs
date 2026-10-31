using System.Text.RegularExpressions;
using FacturAR.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace FacturAR.Application.ApiKeys;

public sealed class ApiKeyAuthenticator : IApiKeyAuthenticator
{
    private const int LongitudMaximaValor = 200;
    private const int LongitudHashHex = 64;

    private readonly IApiKeyRepository _apiKeys;
    private readonly IContribuyenteRepository _contribuyentes;
    private readonly IApiKeyHasher _hasher;
    private readonly int _longitudPrefijoFormato;
    private readonly int _longitudPrefijo;
    private readonly Regex _formatoValor;
    private readonly ILogger<ApiKeyAuthenticator> _logger;

    public ApiKeyAuthenticator(
        IApiKeyRepository apiKeys,
        IContribuyenteRepository contribuyentes,
        IApiKeyHasher hasher,
        ApiKeysOptions options,
        ILogger<ApiKeyAuthenticator> logger)
    {
        _apiKeys = apiKeys;
        _contribuyentes = contribuyentes;
        _hasher = hasher;
        _logger = logger;
        _longitudPrefijoFormato = options.PrefijoFormato.Length;
        _longitudPrefijo = 2 * (Math.Max(2, options.LongitudPrefijoHex) / 2);
        _formatoValor = CrearFormatoValor(options, _longitudPrefijo);
    }

    public async Task<ResultadoApiKey?> AutenticarAsync(
        string valorPresentado,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(valorPresentado)
            || valorPresentado.Length > LongitudMaximaValor
            || !_formatoValor.IsMatch(valorPresentado))
        {
            return null;
        }

        var prefix = valorPresentado.Substring(_longitudPrefijoFormato, _longitudPrefijo);

        var apiKey = await _apiKeys.GetByPrefixAsync(prefix, ct);
        if (apiKey is null || !apiKey.Activa)
        {
            return null;
        }

        if (!HashTieneFormatoEsperado(apiKey.Hash))
        {
            _logger.LogError(
                "El hash de la API key {ApiKeyId} tiene un formato inesperado.",
                apiKey.Id);
            return null;
        }

        if (!_hasher.Matches(valorPresentado, apiKey.Hash))
        {
            return null;
        }

        var contribuyente = await _contribuyentes.GetByIdAsync(apiKey.ContribuyenteId, ct);
        if (contribuyente is null || !contribuyente.Activo)
        {
            return null;
        }

        return new ResultadoApiKey(apiKey.Id, apiKey.ContribuyenteId);
    }

    private static Regex CrearFormatoValor(ApiKeysOptions options, int longitudPrefijo)
    {
        var prefijo = Regex.Escape(options.PrefijoFormato);
        var longitudSecreto = (4 * Math.Max(1, options.BytesSecreto) + 2) / 3;
        var patron = $"\\A{prefijo}[0-9a-f]{{{longitudPrefijo}}}_[A-Za-z0-9_-]{{{longitudSecreto}}}\\z";

        return new Regex(patron, RegexOptions.Compiled | RegexOptions.CultureInvariant);
    }

    private static bool HashTieneFormatoEsperado(string? hash)
        => hash is { Length: LongitudHashHex } && hash.All(EsHexMinuscula);

    private static bool EsHexMinuscula(char caracter)
        => caracter is >= '0' and <= '9' or >= 'a' and <= 'f';
}
