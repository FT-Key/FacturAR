using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using FacturAR.Application.ApiKeys;
using FacturAR.Contracts.Comunes;
using FacturAR.Domain.Entities;
using FacturAR.Infrastructure.Mongo;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace FacturAR.IntegrationTests;

public sealed class MongoApiKeyAuthenticationTests : IAsyncLifetime
{
    private static readonly ApiKeysOptions Options = new()
    {
        HeaderName = "X-Api-Key",
        ActualizarUltimoUso = true,
        IntervaloActualizacionUltimoUsoMinutos = 60,
        TimeoutActualizacionUltimoUsoMs = 5000
    };

    private readonly string _databaseName = $"facturar_it_{Guid.NewGuid():N}"[..28];
    private readonly ApiKeyHasher _hasher = new();

    private ApiKeyStubHost? _host;
    private IMongoClient? _mongoClient;

    public async Task InitializeAsync()
    {
        if (string.IsNullOrWhiteSpace(MongoTestSettings.ConnectionString))
        {
            return;
        }

        _host = await ApiKeyStubHost.CrearConMongoAsync(
            MongoTestSettings.GetConnectionStringWithTimeout(),
            _databaseName,
            Options,
            fallarActualizacion: false,
            contribuyenteCookie: Guid.NewGuid());

        _mongoClient = _host.Services.GetRequiredService<IMongoClient>();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await MongoIndexInitializer.InitializeAsync(
            _host.Services.GetRequiredService<MongoDbContext>(),
            timeout.Token);
    }

    public async Task DisposeAsync()
    {
        _mongoClient?.DropDatabase(_databaseName);

        if (_host is not null)
        {
            await _host.DisposeAsync();
        }
    }

    [MongoFact]
    public async Task CP01_KeyValida_AutenticaContraElEndpointProtegidoYResuelveElContribuyente()
    {
        var contribuyente = await NuevoContribuyenteAsync();
        var (_, valor) = await NuevaKeyAsync(contribuyente.Id);

        var response = await EnviarConKeyAsync("/stub/negocio", valor);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<TenantResuelto>();
        Assert.Equal(contribuyente.Id, body!.ContribuyenteId);
    }

    [MongoFact]
    public async Task CP05_KeyRevocada_Devuelve401ApiKeyInvalida()
    {
        var contribuyente = await NuevoContribuyenteAsync();
        var (_, valor) = await NuevaKeyAsync(contribuyente.Id, activa: false);

        var response = await EnviarConKeyAsync("/stub/negocio", valor);

        await AssertErrorAsync(response, HttpStatusCode.Unauthorized, "API_KEY_INVALIDA");
    }

    [MongoFact]
    public async Task CP06_ContribuyenteInactivo_Devuelve401ApiKeyInvalida()
    {
        var contribuyente = await NuevoContribuyenteAsync(activo: false);
        var (_, valor) = await NuevaKeyAsync(contribuyente.Id);

        var response = await EnviarConKeyAsync("/stub/negocio", valor);

        await AssertErrorAsync(response, HttpStatusCode.Unauthorized, "API_KEY_INVALIDA");
    }

    [MongoFact]
    public async Task CP07_HashAlteradoEnUnCaracter_Devuelve401ApiKeyInvalida()
    {
        var contribuyente = await NuevoContribuyenteAsync();
        var (_, valor) = await NuevaKeyAsync(contribuyente.Id);
        var alterado = valor[..^1] + (valor[^1] == 'A' ? 'B' : 'A');

        var response = await EnviarConKeyAsync("/stub/negocio", alterado);

        await AssertErrorAsync(response, HttpStatusCode.Unauthorized, "API_KEY_INVALIDA");
    }

    [MongoFact]
    public async Task CP10_TenantSaleDelClaimYNoDelQueryNiDelBody()
    {
        var empresaA = await NuevoContribuyenteAsync();
        var empresaB = await NuevoContribuyenteAsync();
        var (_, valor) = await NuevaKeyAsync(empresaA.Id);

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/stub/negocio?contribuyenteId={empresaB.Id}")
        {
            Content = new StringContent(
                $"{{\"contribuyenteId\":\"{empresaB.Id}\"}}",
                Encoding.UTF8,
                "application/json")
        };
        request.Headers.TryAddWithoutValidation("X-Api-Key", valor);

        var response = await Host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<TenantResuelto>();
        Assert.Equal(empresaA.Id, body!.ContribuyenteId);
        Assert.NotEqual(empresaB.Id, body.ContribuyenteId);
    }

    [MongoFact]
    public async Task CP11_UltimoUsoDentroDelIntervalo_NoSeSobrescribe()
    {
        var contribuyente = await NuevoContribuyenteAsync();
        var (key, valor) = await NuevaKeyAsync(
            contribuyente.Id,
            ultimoUso: DateTime.UtcNow.AddSeconds(-10));
        var original = (await LeerKeyAsync(key.Id)).UltimoUso!.Value;

        var response = await EnviarConKeyAsync("/stub/negocio", valor);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await Task.Delay(TimeSpan.FromMilliseconds(750));

        var actual = (await LeerKeyAsync(key.Id)).UltimoUso;
        Assert.Equal(original, actual);
    }

    [MongoFact]
    public async Task CP12_KeyNuncaUsada_ActualizaUltimoUsoConHoraUtc()
    {
        var contribuyente = await NuevoContribuyenteAsync();
        var (key, valor) = await NuevaKeyAsync(contribuyente.Id);
        Assert.Null((await LeerKeyAsync(key.Id)).UltimoUso);
        var antes = DateTime.UtcNow;

        var response = await EnviarConKeyAsync("/stub/negocio", valor);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await EsperarAsync(async () => (await LeerKeyAsync(key.Id)).UltimoUso is not null);

        var ultimoUso = (await LeerKeyAsync(key.Id)).UltimoUso!.Value;
        Assert.Equal(DateTimeKind.Utc, ultimoUso.Kind);
        Assert.InRange(ultimoUso, antes.AddMinutes(-1), DateTime.UtcNow.AddMinutes(1));
    }

    [MongoFact]
    public async Task CP13_FalloDeActualizacion_NoBloqueaLaRespuestaYRegistraWarning()
    {
        var contribuyente = await NuevoContribuyenteAsync();
        var (_, valor) = await NuevaKeyAsync(contribuyente.Id);

        await using var hostFallido = await ApiKeyStubHost.CrearConMongoAsync(
            MongoTestSettings.GetConnectionStringWithTimeout(),
            _databaseName,
            Options,
            fallarActualizacion: true,
            contribuyenteCookie: Guid.NewGuid());

        using var request = new HttpRequestMessage(HttpMethod.Get, "/stub/negocio");
        request.Headers.TryAddWithoutValidation("X-Api-Key", valor);

        var response = await hostFallido.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await EsperarAsync(() => Task.FromResult(hostFallido.Logs.Entradas.Any(
            e => e.Nivel >= LogLevel.Warning
                && e.Mensaje.Contains("No se pudo actualizar", StringComparison.Ordinal))));
    }

    [MongoFact]
    public async Task CP14_LosLogsYLasRespuestasNoContienenLaKeyNiElHash()
    {
        var contribuyente = await NuevoContribuyenteAsync();
        var (key, valor) = await NuevaKeyAsync(contribuyente.Id);
        var (_, valorRevocado) = await NuevaKeyAsync(contribuyente.Id, activa: false);

        var ok = await EnviarConKeyAsync("/stub/negocio", valor);
        var rechazado = await EnviarConKeyAsync("/stub/negocio", valorRevocado);

        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, rechazado.StatusCode);

        await Task.Delay(TimeSpan.FromMilliseconds(750));

        var mensajes = Host.Logs.Entradas.Select(e => e.Mensaje).ToList();
        var cuerpos = string.Join(
            '\n',
            await ok.Content.ReadAsStringAsync(),
            await rechazado.Content.ReadAsStringAsync());

        Assert.DoesNotContain(mensajes, m => m.Contains(valor, StringComparison.Ordinal));
        Assert.DoesNotContain(mensajes, m => m.Contains(valorRevocado, StringComparison.Ordinal));
        Assert.DoesNotContain(mensajes, m => m.Contains(key.Hash, StringComparison.Ordinal));
        Assert.DoesNotContain(valor, cuerpos, StringComparison.Ordinal);
        Assert.DoesNotContain(valorRevocado, cuerpos, StringComparison.Ordinal);
        Assert.DoesNotContain(key.Hash, cuerpos, StringComparison.Ordinal);
    }

    private ApiKeyStubHost Host => _host
        ?? throw new InvalidOperationException("El test de MongoDB no fue inicializado.");

    private async Task<Contribuyente> NuevoContribuyenteAsync(bool activo = true)
    {
        var contribuyente = new Contribuyente
        {
            RazonSocial = "Demo SRL",
            Cuit = Random.Shared
                .NextInt64(10_000_000_000L, 99_999_999_999L)
                .ToString(CultureInfo.InvariantCulture),
            Activo = activo
        };

        await Contribuyentes().InsertOneAsync(contribuyente);
        return contribuyente;
    }

    private async Task<(ApiKey Key, string Valor)> NuevaKeyAsync(
        Guid contribuyenteId,
        bool activa = true,
        DateTime? ultimoUso = null)
    {
        var prefix = Guid.NewGuid().ToString("N")[..12];
        var valor = $"fak_{prefix}_{new string('A', 43)}";
        var key = new ApiKey
        {
            ContribuyenteId = contribuyenteId,
            Nombre = "Integración",
            Prefix = prefix,
            Hash = _hasher.Hash(valor),
            Activa = activa,
            UltimoUso = ultimoUso
        };

        await ApiKeys().InsertOneAsync(key);
        return (key, valor);
    }

    private async Task<HttpResponseMessage> EnviarConKeyAsync(string ruta, string valor)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, ruta);
        request.Headers.TryAddWithoutValidation("X-Api-Key", valor);
        return await Host.Client.SendAsync(request);
    }

    private async Task<ApiKey> LeerKeyAsync(Guid id)
        => await ApiKeys().Find(a => a.Id == id).SingleAsync();

    private static async Task AssertErrorAsync(
        HttpResponseMessage response,
        HttpStatusCode status,
        string codigo)
    {
        Assert.Equal(status, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal(codigo, error!.Codigo);
    }

    private static async Task EsperarAsync(Func<Task<bool>> condicion, int timeoutMs = 5000)
    {
        var inicio = Environment.TickCount64;

        while (!await condicion())
        {
            if (Environment.TickCount64 - inicio > timeoutMs)
            {
                throw new TimeoutException("No se cumplió la condición dentro del tiempo esperado.");
            }

            await Task.Delay(50);
        }
    }

    private IMongoCollection<ApiKey> ApiKeys()
        => _mongoClient!.GetDatabase(_databaseName).GetCollection<ApiKey>(MongoCollections.ApiKeys);

    private IMongoCollection<Contribuyente> Contribuyentes()
        => _mongoClient!
            .GetDatabase(_databaseName)
            .GetCollection<Contribuyente>(MongoCollections.Contribuyentes);
}
