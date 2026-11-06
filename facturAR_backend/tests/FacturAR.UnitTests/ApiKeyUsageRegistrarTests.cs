using System.Diagnostics;
using FacturAR.Api.Auth;
using FacturAR.Application.Abstractions;
using FacturAR.Application.ApiKeys;
using FacturAR.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FacturAR.UnitTests;

public sealed class ApiKeyUsageRegistrarTests
{
    [Fact]
    public void Registrar_ActualizarUltimoUsoDesactivado_NoConsultaElRepositorio()
    {
        var contexto = new ApiKeysContexto();
        var key = contexto.AgregarKey();
        var options = new ApiKeysOptions { ActualizarUltimoUso = false };
        var registrar = CrearRegistrador(contexto.ApiKeys, options, out _);

        registrar.Registrar(key.Id, key.ContribuyenteId);

        Assert.Equal(0, contexto.ApiKeys.IntentosActualizacionUltimoUso);
    }

    [Fact]
    public async Task Registrar_UltimoUsoDentroDelIntervalo_NoEscribe()
    {
        var contexto = new ApiKeysContexto();
        var key = contexto.AgregarKey();
        var ultimoUsoOriginal = DateTime.UtcNow.AddSeconds(-10);
        key.UltimoUso = ultimoUsoOriginal;
        var options = new ApiKeysOptions
        {
            ActualizarUltimoUso = true,
            IntervaloActualizacionUltimoUsoMinutos = 60
        };
        var registrar = CrearRegistrador(contexto.ApiKeys, options, out _);

        registrar.Registrar(key.Id, key.ContribuyenteId);

        await EsperarAsync(() => contexto.ApiKeys.IntentosActualizacionUltimoUso >= 1);
        Assert.Equal(0, contexto.ApiKeys.ActualizacionesUltimoUso);
        Assert.Equal(ultimoUsoOriginal, key.UltimoUso);
    }

    [Fact]
    public async Task Registrar_KeyNuncaUsada_EscribeLaHoraUtc()
    {
        var contexto = new ApiKeysContexto();
        var key = contexto.AgregarKey();
        Assert.Null(key.UltimoUso);
        var options = new ApiKeysOptions { ActualizarUltimoUso = true };
        var registrar = CrearRegistrador(contexto.ApiKeys, options, out _);
        var antes = DateTime.UtcNow;

        registrar.Registrar(key.Id, key.ContribuyenteId);

        await EsperarAsync(() => contexto.ApiKeys.ActualizacionesUltimoUso == 1);
        Assert.NotNull(key.UltimoUso);
        Assert.Equal(DateTimeKind.Utc, key.UltimoUso!.Value.Kind);
        Assert.InRange(key.UltimoUso.Value, antes, DateTime.UtcNow);
    }

    [Fact]
    public async Task Registrar_RepositorioLanzaExcepcion_NoPropagaYRegistraWarning()
    {
        var keyId = Guid.NewGuid();
        var contribuyenteId = Guid.NewGuid();
        var repositorio = new SpyApiKeyRepository
        {
            AlActualizar = (_, _, _, _, _) => throw new TimeoutException("Mongo no responde")
        };
        var options = new ApiKeysOptions { ActualizarUltimoUso = true };
        var registrar = CrearRegistrador(repositorio, options, out var logger);

        var excepcion = Record.Exception(() => registrar.Registrar(keyId, contribuyenteId));

        Assert.Null(excepcion);
        await EsperarAsync(() => logger.Entradas.Any(e => e.Nivel == LogLevel.Warning));
        Assert.Equal(1, repositorio.Llamadas);
        Assert.IsType<TimeoutException>(repositorio.Excepcion);
        Assert.Contains(
            logger.Entradas,
            e => e.Nivel == LogLevel.Warning && e.Mensaje.Contains("No se pudo actualizar", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Registrar_TimeoutConfigurado_NoRompeLaAutenticacion()
    {
        var repositorio = new SpyApiKeyRepository
        {
            AlActualizar = async (_, _, _, _, ct) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, ct);
                return true;
            }
        };
        var options = new ApiKeysOptions
        {
            ActualizarUltimoUso = true,
            TimeoutActualizacionUltimoUsoMs = 25
        };
        var registrar = CrearRegistrador(repositorio, options, out var logger);
        var cronometro = Stopwatch.StartNew();

        registrar.Registrar(Guid.NewGuid(), Guid.NewGuid());
        cronometro.Stop();

        Assert.True(cronometro.Elapsed < TimeSpan.FromSeconds(2));
        await EsperarAsync(() => logger.Entradas.Any(e => e.Nivel == LogLevel.Warning));
        Assert.Equal(1, repositorio.Llamadas);
    }

    private static ApiKeyUsageRegistrar CrearRegistrador(
        IApiKeyRepository repositorio,
        ApiKeysOptions options,
        out RegistroConNiveles<ApiKeyUsageRegistrar> logger)
    {
        logger = new RegistroConNiveles<ApiKeyUsageRegistrar>();
        var services = new ServiceCollection();
        services.AddSingleton(repositorio);
        var provider = services.BuildServiceProvider();

        return new ApiKeyUsageRegistrar(
            provider.GetRequiredService<IServiceScopeFactory>(),
            logger,
            options);
    }

    private static async Task EsperarAsync(Func<bool> condicion, int timeoutMs = 5000)
    {
        var inicio = Environment.TickCount64;

        while (!condicion())
        {
            if (Environment.TickCount64 - inicio > timeoutMs)
            {
                throw new TimeoutException("No se cumplió la condición dentro del tiempo esperado.");
            }

            await Task.Delay(20);
        }
    }
}

internal sealed record EntradaRegistro(LogLevel Nivel, string Mensaje, Exception? Excepcion);

internal sealed class RegistroConNiveles<T> : ILogger<T>
{
    public List<EntradaRegistro> Entradas { get; } = [];

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
        => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
        => Entradas.Add(new EntradaRegistro(logLevel, formatter(state, exception), exception));
}

internal sealed class SpyApiKeyRepository : IApiKeyRepository
{
    public Func<Guid, Guid, DateTime, TimeSpan, CancellationToken, Task<bool>>? AlActualizar { get; init; }

    public int Llamadas { get; private set; }

    public Exception? Excepcion { get; private set; }

    public Task<ApiKey?> GetByIdAsync(Guid id, Guid contribuyenteId, CancellationToken ct = default)
        => throw new NotSupportedException();

    public Task<IReadOnlyList<ApiKey>> ListByContribuyenteAsync(
        Guid contribuyenteId,
        CancellationToken ct = default)
        => throw new NotSupportedException();

    public Task<ApiKey?> GetByPrefixAsync(string prefix, CancellationToken ct = default)
        => throw new NotSupportedException();

    public Task InsertAsync(ApiKey apiKey, CancellationToken ct = default)
        => throw new NotSupportedException();

    public Task UpdateAsync(ApiKey apiKey, CancellationToken ct = default)
        => throw new NotSupportedException();

    public async Task<bool> ActualizarUltimoUsoAsync(
        Guid id,
        Guid contribuyenteId,
        DateTime ahoraUtc,
        TimeSpan intervalo,
        CancellationToken ct = default)
    {
        Llamadas++;

        try
        {
            return AlActualizar is null
                ? true
                : await AlActualizar(id, contribuyenteId, ahoraUtc, intervalo, ct);
        }
        catch (Exception ex)
        {
            Excepcion = ex;
            throw;
        }
    }
}
