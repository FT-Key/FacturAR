using FacturAR.Application.Auth;

namespace FacturAR.Api.Auth;

public sealed class AdminGlobalBootstrapHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AdminGlobalBootstrapHostedService> _logger;

    public AdminGlobalBootstrapHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<AdminGlobalBootstrapHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var bootstrap = scope.ServiceProvider.GetRequiredService<AdminGlobalBootstrapService>();
            await bootstrap.EjecutarAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo ejecutar el bootstrap del administrador global.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}