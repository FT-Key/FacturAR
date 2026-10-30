using FacturAR.Application.ApiKeys;
using Microsoft.Extensions.Logging;

namespace FacturAR.UnitTests;

public sealed class ApiKeyAuthenticatorTests
{
    [Theory]
    [InlineData("no-es-una-key")]
    [InlineData("fak_0123456789ab_corto")]
    [InlineData("FAK_0123456789ab_AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public async Task ValorConFormatoInvalido_DevuelveNull(string valor)
    {
        var contexto = new ApiKeysContexto();
        var autenticador = CrearAutenticador(contexto, out _);

        Assert.Null(await autenticador.AutenticarAsync(valor));
    }

    [Fact]
    public async Task ValorDemasiadoLargo_DevuelveNull()
    {
        var contexto = new ApiKeysContexto();
        var autenticador = CrearAutenticador(contexto, out _);
        var valor = $"fak_0123456789ab_{new string('A', 43)}";

        Assert.Null(await autenticador.AutenticarAsync(valor + "x"));
    }

    [Fact]
    public async Task PrefijoInexistente_DevuelveNull()
    {
        var contexto = new ApiKeysContexto();
        contexto.AgregarKey(prefix: "0123456789ab");
        var autenticador = CrearAutenticador(contexto, out _);

        var resultado = await autenticador.AutenticarAsync($"fak_ffffffffffff_{new string('A', 43)}");

        Assert.Null(resultado);
    }

    [Fact]
    public async Task KeyRevocada_DevuelveNull()
    {
        var contexto = new ApiKeysContexto();
        var key = contexto.AgregarKey(activa: false);
        var autenticador = CrearAutenticador(contexto, out _);

        var resultado = await autenticador.AutenticarAsync(Valor(key.Prefix));

        Assert.Null(resultado);
    }

    [Fact]
    public async Task HashIncorrecto_DevuelveNull()
    {
        var contexto = new ApiKeysContexto();
        var key = contexto.AgregarKey(hash: contexto.Hasher.Hash(Valor("aaaaaaaaaaaa")));
        var autenticador = CrearAutenticador(contexto, out _);

        var resultado = await autenticador.AutenticarAsync(Valor(key.Prefix));

        Assert.Null(resultado);
    }

    [Fact]
    public async Task HashConFormatoInesperado_DevuelveNullYLogueaSinExponerValores()
    {
        var contexto = new ApiKeysContexto();
        var key = contexto.AgregarKey(hash: "no-es-un-hash");
        var autenticador = CrearAutenticador(contexto, out var logger);
        var valor = Valor(key.Prefix);

        var resultado = await autenticador.AutenticarAsync(valor);

        Assert.Null(resultado);
        Assert.Contains(logger.Mensajes, m => m.Contains("formato inesperado", StringComparison.Ordinal));
        Assert.DoesNotContain(logger.Mensajes, m => m.Contains(valor, StringComparison.Ordinal));
        Assert.DoesNotContain(logger.Mensajes, m => m.Contains(key.Hash, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ContribuyenteInactivo_DevuelveNull()
    {
        var contexto = new ApiKeysContexto();
        var key = contexto.AgregarKey();
        contexto.Contribuyente.Activo = false;
        var autenticador = CrearAutenticador(contexto, out _);

        var resultado = await autenticador.AutenticarAsync(Valor(key.Prefix));

        Assert.Null(resultado);
    }

    [Fact]
    public async Task ContribuyenteInexistente_DevuelveNull()
    {
        var contexto = new ApiKeysContexto();
        var key = contexto.AgregarKey(contribuyenteId: Guid.NewGuid());
        var autenticador = CrearAutenticador(contexto, out _);

        var resultado = await autenticador.AutenticarAsync(Valor(key.Prefix));

        Assert.Null(resultado);
    }

    [Fact]
    public async Task SecretoAlteradoEnUnCaracter_DevuelveNull()
    {
        var contexto = new ApiKeysContexto();
        var key = contexto.AgregarKey();
        var autenticador = CrearAutenticador(contexto, out _);
        var valorCorrecto = Valor(key.Prefix);
        var valorAlterado = valorCorrecto[..^1] + (valorCorrecto[^1] == 'A' ? 'B' : 'A');

        Assert.Null(await autenticador.AutenticarAsync(valorAlterado));
    }

    [Fact]
    public async Task ValorVacio_DevuelveNull()
    {
        var contexto = new ApiKeysContexto();
        var autenticador = CrearAutenticador(contexto, out _);

        Assert.Null(await autenticador.AutenticarAsync(string.Empty));
    }

    [Fact]
    public async Task KeyValida_DevuelveApiKeyAutenticadaYNoLogueaElValor()
    {
        var contexto = new ApiKeysContexto();
        var key = contexto.AgregarKey();
        var autenticador = CrearAutenticador(contexto, out var logger);
        var valor = Valor(key.Prefix);

        var resultado = await autenticador.AutenticarAsync(valor);

        Assert.NotNull(resultado);
        Assert.Equal(key.Id, resultado.ApiKeyId);
        Assert.Equal(key.ContribuyenteId, resultado.ContribuyenteId);
        Assert.Empty(logger.Mensajes);
    }

    private static ApiKeyAuthenticator CrearAutenticador(
        ApiKeysContexto contexto,
        out RegistroCaptor<ApiKeyAuthenticator> logger)
    {
        logger = new RegistroCaptor<ApiKeyAuthenticator>();
        return new ApiKeyAuthenticator(
            contexto.ApiKeys,
            contexto.Contribuyentes,
            contexto.Hasher,
            contexto.Options,
            logger);
    }

    private static string Valor(string prefix)
        => $"fak_{prefix}_{new string('A', 43)}";
}

internal sealed class RegistroCaptor<T> : ILogger<T>
{
    private readonly List<string> _mensajes;

    public RegistroCaptor(List<string>? mensajes = null)
    {
        _mensajes = mensajes ?? [];
    }

    public IReadOnlyList<string> Mensajes => _mensajes;

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
        => _mensajes.Add(formatter(state, exception));
}
