using System.Net;
using System.Net.Http.Json;
using System.Text;
using FacturAR.Application.ApiKeys;
using FacturAR.Contracts.Comunes;

namespace FacturAR.IntegrationTests;

public sealed class ApiKeyStubHostTests : IAsyncLifetime
{
    private const string ValorValido = "fak_0123456789ab_AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
    private static readonly Guid ContribuyenteA = Guid.NewGuid();
    private static readonly Guid ContribuyenteB = Guid.NewGuid();
    private static readonly Guid ApiKeyId = Guid.NewGuid();

    private ApiKeyStubHost _host = null!;

    public async Task InitializeAsync()
    {
        _host = await ApiKeyStubHost.CrearConAutenticadorFalsoAsync(
            new Dictionary<string, ResultadoApiKey>(StringComparer.Ordinal)
            {
                [ValorValido] = new ResultadoApiKey(ApiKeyId, ContribuyenteA)
            },
            ContribuyenteA);
    }

    public Task DisposeAsync() => _host.DisposeAsync().AsTask();

    [Fact]
    public async Task EndpointAnonimo_SinApiKey_Devuelve200()
    {
        var response = await _host.Client.GetAsync("/stub/anonimo");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<RespuestaAnonima>();
        Assert.True(body!.Ok);
    }

    [Fact]
    public async Task SinApiKey_Devuelve401ConApiKeyAusente()
    {
        var response = await _host.Client.GetAsync("/stub/negocio");

        await AssertErrorAsync(response, HttpStatusCode.Unauthorized, "API_KEY_AUSENTE");
    }

    [Fact]
    public async Task ApiKeyInvalida_Devuelve401ConApiKeyInvalida()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/stub/negocio");
        request.Headers.TryAddWithoutValidation("X-Api-Key", "no-es-una-key");

        var response = await _host.Client.SendAsync(request);

        await AssertErrorAsync(response, HttpStatusCode.Unauthorized, "API_KEY_INVALIDA");
    }

    [Fact]
    public async Task ApiKeyValida_DevuelveElContribuyenteDelClaim()
    {
        var response = await EnviarConKeyAsync("/stub/negocio", ValorValido);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<TenantResuelto>();
        Assert.Equal(ContribuyenteA, body!.ContribuyenteId);
    }

    [Fact]
    public async Task QueryYBodyConOtroContribuyente_NoCambianElTenant()
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/stub/negocio?contribuyenteId={ContribuyenteB}")
        {
            Content = new StringContent(
                $"{{\"contribuyenteId\":\"{ContribuyenteB}\"}}",
                Encoding.UTF8,
                "application/json")
        };
        request.Headers.TryAddWithoutValidation("X-Api-Key", ValorValido);

        var response = await _host.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<TenantResuelto>();
        Assert.Equal(ContribuyenteA, body!.ContribuyenteId);
    }

    [Fact]
    public async Task CookieDeSesion_NoSatisfaceLaPoliticaDeApiKey()
    {
        var login = await _host.Client.GetAsync("/stub/login-cookie");
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var cookie = login.Headers.GetValues("Set-Cookie").First().Split(';')[0];

        using var request = new HttpRequestMessage(HttpMethod.Get, "/stub/negocio");
        request.Headers.TryAddWithoutValidation("Cookie", cookie);

        var response = await _host.Client.SendAsync(request);

        await AssertErrorAsync(response, HttpStatusCode.Unauthorized, "API_KEY_AUSENTE");
    }

    [Fact]
    public async Task ApiKeyValida_NoApareceEnLosLogs()
    {
        await EnviarConKeyAsync("/stub/negocio", ValorValido);

        Assert.DoesNotContain(
            _host.Logs.Entradas,
            e => e.Mensaje.Contains(ValorValido, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ApiKeyInvalida_ElLogNoExponeElValor()
    {
        const string valor = "fak_0123456789ab_BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB";
        using var request = new HttpRequestMessage(HttpMethod.Get, "/stub/negocio");
        request.Headers.TryAddWithoutValidation("X-Api-Key", valor);

        await _host.Client.SendAsync(request);

        Assert.DoesNotContain(
            _host.Logs.Entradas,
            e => e.Mensaje.Contains(valor, StringComparison.Ordinal));
    }

    private Task<HttpResponseMessage> EnviarConKeyAsync(string ruta, string valor)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, ruta);
        request.Headers.TryAddWithoutValidation("X-Api-Key", valor);
        return _host.Client.SendAsync(request);
    }

    private static async Task AssertErrorAsync(
        HttpResponseMessage response,
        HttpStatusCode status,
        string codigo)
    {
        Assert.Equal(status, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal(codigo, error!.Codigo);
    }
}
