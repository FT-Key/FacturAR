using System.Net;
using System.Net.Http.Json;
using FacturAR.Contracts.Sistema;

namespace FacturAR.IntegrationTests;

public sealed class HealthEndpointsTests
{
    private const string CommitEsperado = "0123456789abcdef0123456789abcdef01234567";

    [Fact]
    public async Task HealthLive_DevuelveOkSinConsultarMongoDb()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<EstadoVivoResponse>();

        Assert.NotNull(payload);
        Assert.Equal("ok", payload.Status);
        Assert.False(string.IsNullOrWhiteSpace(payload.Version));
        Assert.False(string.IsNullOrWhiteSpace(payload.Commit));
    }

    [Fact]
    public async Task HealthLive_InformaElCommitDeLaBuild()
    {
        var settings = new Dictionary<string, string>
        {
            ["SOURCE_COMMIT"] = CommitEsperado,
            ["APP_VERSION"] = "9.9.9"
        };

        using var factory = new ConfiguredWebApplicationFactory(settings);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<EstadoVivoResponse>();

        Assert.NotNull(payload);
        Assert.Equal(CommitEsperado, payload.Commit);
        Assert.Equal("9.9.9", payload.Version);
    }

    [Fact]
    public async Task Raiz_InformaElCommitDeLaBuild()
    {
        var settings = new Dictionary<string, string>
        {
            ["SOURCE_COMMIT"] = CommitEsperado
        };

        using var factory = new ConfiguredWebApplicationFactory(settings);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<RaizResponse>();

        Assert.NotNull(payload);
        Assert.Equal("FacturAR API", payload.Servicio);
        Assert.Equal(CommitEsperado, payload.Commit);
    }

    private sealed record RaizResponse(string Servicio, string Version, string Commit, string Documentacion);
}
