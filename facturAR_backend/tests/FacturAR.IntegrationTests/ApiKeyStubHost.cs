using System.Collections.Concurrent;
using System.Security.Claims;
using FacturAR.Api.Auth;
using FacturAR.Api.Security;
using FacturAR.Application.Abstractions;
using FacturAR.Application.ApiKeys;
using FacturAR.Application.Auth;
using FacturAR.Domain.Entities;
using FacturAR.Infrastructure.Mongo;
using FacturAR.Infrastructure.Repositories;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace FacturAR.IntegrationTests;

internal sealed class ApiKeyStubHost : IAsyncDisposable
{
    private readonly WebApplication _app;

    private ApiKeyStubHost(WebApplication app, HttpClient client, TestLogCollector logs)
    {
        _app = app;
        Client = client;
        Logs = logs;
    }

    public HttpClient Client { get; }

    public TestLogCollector Logs { get; }

    public IServiceProvider Services => _app.Services;

    public static Task<ApiKeyStubHost> CrearConAutenticadorFalsoAsync(
        IReadOnlyDictionary<string, ResultadoApiKey> validas,
        Guid contribuyenteCookie)
        => CrearAsync(
            services =>
            {
                services.AddSingleton<IApiKeyAuthenticator>(new FakeApiKeyAuthenticator(validas));
                services.AddSingleton<ApiKeyUsageRegistrar>();
            },
            new ApiKeysOptions
            {
                HeaderName = "X-Api-Key",
                ActualizarUltimoUso = false
            },
            contribuyenteCookie);

    public static Task<ApiKeyStubHost> CrearConMongoAsync(
        string connectionString,
        string databaseName,
        ApiKeysOptions apiKeysOptions,
        bool fallarActualizacion,
        Guid contribuyenteCookie)
    {
        MongoSerialization.Configure();

        return CrearAsync(
            services =>
            {
                services.AddSingleton<IMongoClient>(_ => new MongoClient(connectionString));
                services.AddSingleton(sp => sp.GetRequiredService<IMongoClient>().GetDatabase(databaseName));
                services.AddSingleton<MongoDbContext>();
                services.AddScoped<MongoSesionActual>();
                services.AddScoped<IContribuyenteRepository, ContribuyenteRepository>();
                services.AddScoped<IApiKeyHasher, ApiKeyHasher>();
                services.AddScoped<IApiKeyAuthenticator, ApiKeyAuthenticator>();
                services.AddSingleton<ApiKeyUsageRegistrar>();

                if (fallarActualizacion)
                {
                    services.AddScoped<ApiKeyRepository>();
                    services.AddScoped<IApiKeyRepository>(
                        sp => new FalloActualizacionApiKeyRepository(sp.GetRequiredService<ApiKeyRepository>()));
                }
                else
                {
                    services.AddScoped<IApiKeyRepository, ApiKeyRepository>();
                }
            },
            apiKeysOptions,
            contribuyenteCookie);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();
    }

    private static async Task<ApiKeyStubHost> CrearAsync(
        Action<IServiceCollection> configurarServicios,
        ApiKeysOptions apiKeysOptions,
        Guid contribuyenteCookie)
    {
        var logs = new TestLogCollector();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.AddProvider(logs);
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton(apiKeysOptions);
        builder.Services.AddScoped<ITenantContext, TenantContext>();
        builder.Services.AddFacturarCookies(new AuthOptions(), isDevelopment: true);
        builder.Services.AddFacturarApiKeyAuthentication();
        configurarServicios(builder.Services);

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapGet("/stub/negocio", (ITenantContext tenant) =>
                Results.Ok(new TenantResuelto(tenant.ContribuyenteId)))
            .RequireApiKey();

        app.MapGet("/stub/anonimo", () => Results.Ok(new RespuestaAnonima(true)))
            .AllowAnonymous();

        app.MapGet("/stub/login-cookie", async (HttpContext http) =>
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new Claim("contribuyenteId", contribuyenteCookie.ToString()),
                new Claim("rol", "Administrador"),
                new Claim("plano", "Empresa")
            };

            await http.SignInAsync(
                CookieAuthenticationExtensions.EmpresaScheme,
                new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationExtensions.EmpresaScheme)));

            return Results.Ok();
        }).AllowAnonymous();

        await app.StartAsync();

        var direccion = app.Services
            .GetRequiredService<IServer>()
            .Features
            .Get<IServerAddressesFeature>()!
            .Addresses
            .First();

        var client = new HttpClient(new HttpClientHandler { UseCookies = false })
        {
            BaseAddress = new Uri(direccion)
        };

        return new ApiKeyStubHost(app, client, logs);
    }

    private sealed class FalloActualizacionApiKeyRepository : IApiKeyRepository
    {
        private readonly ApiKeyRepository _inner;

        public FalloActualizacionApiKeyRepository(ApiKeyRepository inner)
        {
            _inner = inner;
        }

        public Task<ApiKey?> GetByIdAsync(Guid id, Guid contribuyenteId, CancellationToken ct = default)
            => _inner.GetByIdAsync(id, contribuyenteId, ct);

        public Task<IReadOnlyList<ApiKey>> ListByContribuyenteAsync(
            Guid contribuyenteId,
            CancellationToken ct = default)
            => _inner.ListByContribuyenteAsync(contribuyenteId, ct);

        public Task<ApiKey?> GetByPrefixAsync(string prefix, CancellationToken ct = default)
            => _inner.GetByPrefixAsync(prefix, ct);

        public Task InsertAsync(ApiKey apiKey, CancellationToken ct = default)
            => _inner.InsertAsync(apiKey, ct);

        public Task UpdateAsync(ApiKey apiKey, CancellationToken ct = default)
            => _inner.UpdateAsync(apiKey, ct);

        public Task<bool> ActualizarUltimoUsoAsync(
            Guid id,
            Guid contribuyenteId,
            DateTime ahoraUtc,
            TimeSpan intervalo,
            CancellationToken ct = default)
            => throw new TimeoutException("Mongo no responde");
    }

    private sealed class FakeApiKeyAuthenticator : IApiKeyAuthenticator
    {
        private readonly IReadOnlyDictionary<string, ResultadoApiKey> _validas;

        public FakeApiKeyAuthenticator(IReadOnlyDictionary<string, ResultadoApiKey> validas)
        {
            _validas = validas;
        }

        public Task<ResultadoApiKey?> AutenticarAsync(string valorPresentado, CancellationToken ct = default)
            => Task.FromResult(
                _validas.TryGetValue(valorPresentado, out var resultado) ? resultado : null);
    }
}

internal sealed record TenantResuelto(Guid? ContribuyenteId);

internal sealed record RespuestaAnonima(bool Ok);

internal sealed record EntradaLog(string Categoria, LogLevel Nivel, string Mensaje);

internal sealed class TestLogCollector : ILoggerProvider
{
    private readonly ConcurrentQueue<EntradaLog> _entradas = new();

    public IReadOnlyCollection<EntradaLog> Entradas => _entradas;

    public ILogger CreateLogger(string categoryName) => new Colector(categoryName, _entradas);

    public void Dispose()
    {
    }

    private sealed class Colector : ILogger
    {
        private readonly string _categoria;
        private readonly ConcurrentQueue<EntradaLog> _entradas;

        public Colector(string categoria, ConcurrentQueue<EntradaLog> entradas)
        {
            _categoria = categoria;
            _entradas = entradas;
        }

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
            => _entradas.Enqueue(new EntradaLog(_categoria, logLevel, formatter(state, exception)));
    }
}
