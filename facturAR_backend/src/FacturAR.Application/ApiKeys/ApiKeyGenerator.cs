using System.Buffers.Text;
using System.Security.Cryptography;
using FacturAR.Application.Abstractions;

namespace FacturAR.Application.ApiKeys;

public sealed class ApiKeyGenerator : IApiKeyGenerator
{
    private readonly ApiKeysOptions _options;

    public ApiKeyGenerator(ApiKeysOptions options)
    {
        _options = options;
    }

    public ApiKeyGeneratorValue Generar()
    {
        var bytesPrefijo = Math.Max(2, _options.LongitudPrefijoHex) / 2;
        var prefix = Convert.ToHexString(RandomNumberGenerator.GetBytes(bytesPrefijo)).ToLowerInvariant();
        var bytesSecreto = Math.Max(1, _options.BytesSecreto);
        var secret = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(bytesSecreto));
        var valor = $"{_options.PrefijoFormato}{prefix}_{secret}";

        return new ApiKeyGeneratorValue(prefix, secret, valor);
    }
}
