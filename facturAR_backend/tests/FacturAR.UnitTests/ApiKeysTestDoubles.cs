using FacturAR.Application.Abstractions;
using FacturAR.Application.ApiKeys;
using FacturAR.Domain.Entities;
using FacturAR.Domain.Exceptions;

namespace FacturAR.UnitTests;

internal sealed class FakeApiKeyRepository : IApiKeyRepository
{
    public List<ApiKey> ApiKeys { get; } = [];

    public HashSet<string> PrefijosEnUso { get; } = [];

    public int IntentosInsercion { get; private set; }

    public int Actualizaciones { get; private set; }

    public int ActualizacionesUltimoUso { get; private set; }

    public int IntentosActualizacionUltimoUso { get; private set; }

    public int ConsultasPorPrefijo { get; private set; }

    public Task<ApiKey?> GetByIdAsync(Guid id, Guid contribuyenteId, CancellationToken ct = default)
        => Task.FromResult(ApiKeys.FirstOrDefault(a => a.Id == id && a.ContribuyenteId == contribuyenteId));

    public Task<IReadOnlyList<ApiKey>> ListByContribuyenteAsync(
        Guid contribuyenteId,
        CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ApiKey>>(
            ApiKeys.Where(a => a.ContribuyenteId == contribuyenteId).ToList());

    public Task<ApiKey?> GetByPrefixAsync(string prefix, CancellationToken ct = default)
    {
        ConsultasPorPrefijo++;
        return Task.FromResult(ApiKeys.FirstOrDefault(a => a.Prefix == prefix));
    }

    public Task InsertAsync(ApiKey apiKey, CancellationToken ct = default)
    {
        IntentosInsercion++;

        if (PrefijosEnUso.Contains(apiKey.Prefix) || ApiKeys.Any(a => a.Prefix == apiKey.Prefix))
        {
            throw new ApiKeyPrefijoDuplicadoException();
        }

        ApiKeys.Add(apiKey);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(ApiKey apiKey, CancellationToken ct = default)
    {
        var existente = ApiKeys.FirstOrDefault(
            a => a.Id == apiKey.Id && a.ContribuyenteId == apiKey.ContribuyenteId);

        if (existente is null)
        {
            throw new ApiKeyNoEncontradaException();
        }

        existente.Nombre = apiKey.Nombre;
        existente.Prefix = apiKey.Prefix;
        existente.Hash = apiKey.Hash;
        existente.Activa = apiKey.Activa;
        existente.RevocadaAt = apiKey.RevocadaAt;
        existente.RegeneradaAt = apiKey.RegeneradaAt;
        Actualizaciones++;

        return Task.CompletedTask;
    }

    public Task<bool> ActualizarUltimoUsoAsync(
        Guid id,
        Guid contribuyenteId,
        DateTime ahoraUtc,
        TimeSpan intervalo,
        CancellationToken ct = default)
    {
        IntentosActualizacionUltimoUso++;

        var existente = ApiKeys.FirstOrDefault(
            a => a.Id == id && a.ContribuyenteId == contribuyenteId);

        if (existente is null || (existente.UltimoUso is not null && existente.UltimoUso >= ahoraUtc - intervalo))
        {
            return Task.FromResult(false);
        }

        existente.UltimoUso = ahoraUtc;
        ActualizacionesUltimoUso++;
        return Task.FromResult(true);
    }
}

internal sealed class FakeContribuyentes : IContribuyenteRepository
{
    public List<Contribuyente> Contribuyentes { get; } = [];

    public Task<Contribuyente?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(Contribuyentes.FirstOrDefault(c => c.Id == id));

    public Task<bool> ExistePorCuitAsync(string cuit, CancellationToken ct = default)
        => Task.FromResult(Contribuyentes.Any(c => c.Cuit == cuit));

    public Task InsertarAsync(Contribuyente contribuyente, CancellationToken ct = default)
    {
        Contribuyentes.Add(contribuyente);
        return Task.CompletedTask;
    }
}

internal sealed class FakeEventos : IEventoAuditoriaRepository
{
    public List<EventoAuditoria> Eventos { get; } = [];

    public Task AddAsync(EventoAuditoria evento, CancellationToken ct = default)
    {
        Eventos.Add(evento);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<EventoAuditoria>> ListarPorEntidadAsync(
        string entidad,
        string entidadId,
        CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<EventoAuditoria>>(
            Eventos.Where(e => e.Entidad == entidad && e.EntidadId == entidadId).ToList());
}

internal sealed class FakeApiKeyGenerator : IApiKeyGenerator
{
    private readonly Func<int, ApiKeyGeneratorValue> _generar;

    public FakeApiKeyGenerator(Func<int, ApiKeyGeneratorValue>? generar = null)
    {
        _generar = generar ?? GenerarPorDefecto;
    }

    public int Llamadas { get; private set; }

    public ApiKeyGeneratorValue Generar()
    {
        Llamadas++;
        return _generar(Llamadas);
    }

    public static ApiKeyGeneratorValue Valor(int numero)
    {
        var prefix = numero.ToString("x12", System.Globalization.CultureInfo.InvariantCulture);
        var secret = new string((char)('A' + (numero % 26)), 43);
        return new ApiKeyGeneratorValue(prefix, secret, $"fak_{prefix}_{secret}");
    }

    private static ApiKeyGeneratorValue GenerarPorDefecto(int numero) => Valor(numero);
}

internal sealed class ApiKeysContexto
{
    public ApiKeysContexto(
        Func<int, ApiKeyGeneratorValue>? generar = null,
        ApiKeysOptions? options = null)
    {
        Options = options ?? new ApiKeysOptions();
        Generator = new FakeApiKeyGenerator(generar);
        Contribuyente = new Contribuyente
        {
            RazonSocial = "Demo SRL",
            Cuit = "20123456786",
            Activo = true
        };
        Contribuyentes.Contribuyentes.Add(Contribuyente);

        Crear = new CrearApiKeyService(
            ApiKeys,
            Contribuyentes,
            Generator,
            Hasher,
            Eventos,
            Options,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<CrearApiKeyService>.Instance);
        Listar = new ListarApiKeysService(ApiKeys, Options);
        Revocar = new RevocarApiKeyService(ApiKeys, Eventos);
        Regenerar = new RegenerarApiKeyService(
            ApiKeys,
            Generator,
            Hasher,
            Eventos,
            Options,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<RegenerarApiKeyService>.Instance);
    }

    public ApiKeysOptions Options { get; }

    public FakeApiKeyRepository ApiKeys { get; } = new();

    public FakeContribuyentes Contribuyentes { get; } = new();

    public FakeEventos Eventos { get; } = new();

    public FakeApiKeyGenerator Generator { get; }

    public ApiKeyHasher Hasher { get; } = new();

    public Contribuyente Contribuyente { get; }

    public Guid UsuarioId { get; } = Guid.NewGuid();

    public CrearApiKeyService Crear { get; }

    public ListarApiKeysService Listar { get; }

    public RevocarApiKeyService Revocar { get; }

    public RegenerarApiKeyService Regenerar { get; }

    public ApiKey AgregarKey(
        Guid? contribuyenteId = null,
        string nombre = "Ecommerce",
        string prefix = "0123456789ab",
        string? hash = null,
        bool activa = true,
        DateTime? revocadaAt = null)
    {
        var apiKey = new ApiKey
        {
            ContribuyenteId = contribuyenteId ?? Contribuyente.Id,
            Nombre = nombre,
            Prefix = prefix,
            Hash = hash ?? Hasher.Hash($"fak_{prefix}_{new string('A', 43)}"),
            Activa = activa,
            RevocadaAt = revocadaAt
        };
        ApiKeys.ApiKeys.Add(apiKey);
        return apiKey;
    }

    public void AssertEventoSinSecretos(EventoAuditoria evento, string accion, string valor)
    {
        Assert.Equal(accion, evento.Accion);
        Assert.Equal(nameof(ApiKey), evento.Entidad);
        Assert.DoesNotContain(valor, evento.Datos ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain(Hasher.Hash(valor), evento.Datos ?? string.Empty, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", evento.Datos ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hash", evento.Datos ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("valor", evento.Datos ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }
}
