using FacturAR.Application.ApiKeys;

namespace FacturAR.Application.Abstractions;

public interface IApiKeyAuthenticator
{
    Task<ResultadoApiKey?> AutenticarAsync(string valorPresentado, CancellationToken ct = default);
}
