using FacturAR.Application.Abstractions;
using FacturAR.Application.ApiKeys;

namespace FacturAR.Api.Auth;

public sealed class ApiKeyUsageRegistrar
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ApiKeyUsageRegistrar> _logger;
    private readonly ApiKeysOptions _options;

    public ApiKeyUsageRegistrar(
        IServiceScopeFactory scopeFactory,
        ILogger<ApiKeyUsageRegistrar> logger,
        ApiKeysOptions options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options;
    }

    public void Registrar(Guid apiKeyId, Guid contribuyenteId)
    {
        if (!_options.ActualizarUltimoUso)
        {
            return;
        }

        var intervalo = TimeSpan.FromMinutes(Math.Max(0, _options.IntervaloActualizacionUltimoUsoMinutos));
        var timeout = TimeSpan.FromMilliseconds(Math.Max(1, _options.TimeoutActualizacionUltimoUsoMs));

        _ = Task.Run(
            () => ActualizarAsync(apiKeyId, contribuyenteId, intervalo, timeout),
            CancellationToken.None);
    }

    private async Task ActualizarAsync(
        Guid apiKeyId,
        Guid contribuyenteId,
        TimeSpan intervalo,
        TimeSpan timeout)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var apiKeys = scope.ServiceProvider.GetRequiredService<IApiKeyRepository>();
            using var timeoutCts = new CancellationTokenSource(timeout);

            await apiKeys.ActualizarUltimoUsoAsync(
                apiKeyId,
                contribuyenteId,
                DateTime.UtcNow,
                intervalo,
                timeoutCts.Token);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "No se pudo actualizar el último uso de la API key {ApiKeyId}.",
                apiKeyId);
        }
    }
}
