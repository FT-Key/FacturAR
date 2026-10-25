using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace FacturAR.IntegrationTests;

public sealed class ApiEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ApiEndpointsTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Raiz_DevuelveOkConInformacionDelServicio()
    {
        var response = await _client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<ServicioResponse>();

        Assert.NotNull(payload);
        Assert.Equal("FacturAR API", payload.Servicio);
        Assert.Equal("/openapi/v1.json", payload.Documentacion);
    }

    [Fact]
    public async Task OpenApi_EnDevelopment_ExponeElDocumento()
    {
        var response = await _client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.True(document.RootElement.TryGetProperty("openapi", out _));
        Assert.True(document.RootElement.TryGetProperty("paths", out var paths));
        Assert.True(paths.TryGetProperty("/v1/comprobantes/verificar/{cae}", out _));
    }

    [Theory]
    [InlineData("123")]
    [InlineData("1234567890123")]
    [InlineData("123456789012345")]
    [InlineData("1234567890123A")]
    public async Task Verificar_ConCaeInvalido_DevuelveBadRequest(string cae)
    {
        var response = await _client.GetAsync($"/v1/comprobantes/verificar/{cae}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<ErrorResponse>();

        Assert.NotNull(payload);
        Assert.Equal("CAE_INVALIDO", payload.Codigo);
    }

    private sealed record ServicioResponse(string Servicio, string Version, string Documentacion);

    private sealed record ErrorResponse(string Codigo, string Mensaje);
}
