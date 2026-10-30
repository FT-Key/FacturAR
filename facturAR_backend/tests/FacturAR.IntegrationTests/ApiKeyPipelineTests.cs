using System.Net;
using System.Net.Http.Json;
using System.Text;
using FacturAR.Contracts.Comunes;

namespace FacturAR.IntegrationTests;

public sealed class ApiKeyPipelineTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ApiKeyPipelineTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task RaizYHealthLive_SinApiKey_RespondenOk()
    {
        var raiz = await _client.GetAsync("/");
        var vivo = await _client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, raiz.StatusCode);
        Assert.Equal(HttpStatusCode.OK, vivo.StatusCode);
    }

    [Fact]
    public async Task VerificarCae_SinApiKey_NoExigeElHeaderYValidaElCae()
    {
        var response = await _client.GetAsync("/v1/comprobantes/verificar/123");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("CAE_INVALIDO", error!.Codigo);
    }

    [Fact]
    public async Task LoginEmpresa_SinApiKey_LlegaAlEndpointDeCookie()
    {
        using var content = new StringContent("{}", Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/v1/auth/login", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("VALIDACION", error!.Codigo);
    }

    [Fact]
    public async Task LoginAdmin_SinApiKey_LlegaAlEndpointDeCookie()
    {
        using var content = new StringContent("{}", Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/v1/admin/auth/login", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("VALIDACION", error!.Codigo);
    }

    [Fact]
    public async Task SolicitudAlta_SinApiKey_LlegaAlEndpointAnonimo()
    {
        using var content = new StringContent("{}", Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/v1/solicitudes-alta", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("VALIDACION", error!.Codigo);
    }
}
