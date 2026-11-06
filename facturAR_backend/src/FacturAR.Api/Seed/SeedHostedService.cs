using FacturAR.Application.Seed;

namespace FacturAR.Api.Seed;

public sealed class SeedHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SeedOptions _options;
    private readonly SeedApiKeyDemoFile _archivo;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<SeedHostedService> _logger;

    public SeedHostedService(
        IServiceScopeFactory scopeFactory,
        SeedOptions options,
        SeedApiKeyDemoFile archivo,
        IHostEnvironment environment,
        ILogger<SeedHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _archivo = archivo;
        _environment = environment;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return;
        }

        if (_environment.IsProduction())
        {
            _logger.LogWarning("Seed__Enabled=true en Production: se cargarán datos demo.");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        var token = timeout.Token;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var seed = scope.ServiceProvider.GetRequiredService<SeedService>();
            var entorno = _environment.IsDevelopment()
                ? SeedEnvironment.Development
                : SeedEnvironment.Production;

            var resultado = await seed.EjecutarAsync(entorno, token);

            if (resultado.Error is not null)
            {
                _logger.LogWarning("Seed omitido: {Error}", resultado.Error);
                return;
            }

            if (!resultado.Ejecutado)
            {
                return;
            }

            if (resultado.ApiKeyDemoValor is not null)
            {
                await _archivo.EscribirAsync(resultado.ApiKeyDemoValor, token);
                _logger.LogInformation("API key demo escrita en {Archivo}.", _archivo.Ruta);
            }

            _logger.LogInformation("Datos demo cargados.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo ejecutar el seed de datos demo.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
