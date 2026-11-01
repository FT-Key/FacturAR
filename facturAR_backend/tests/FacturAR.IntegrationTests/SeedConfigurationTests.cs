using FacturAR.Application.Seed;
using Microsoft.Extensions.DependencyInjection;

namespace FacturAR.IntegrationTests;

public sealed class SeedConfigurationTests
{
    [Fact]
    public void Seed_SinConfiguracion_EstaDeshabilitadoYNoCreaApiKey()
    {
        using var factory = new ConfiguredWebApplicationFactory([]);

        var seed = factory.Services.GetRequiredService<SeedOptions>();

        Assert.False(seed.Enabled);
        Assert.False(seed.ApiKeyDemo);
    }

    [Fact]
    public void SeedEnabled_SeLeeDesdeConfiguracion()
    {
        using var factory = new ConfiguredWebApplicationFactory(new Dictionary<string, string>
        {
            ["Seed:Enabled"] = "true"
        });

        var seed = factory.Services.GetRequiredService<SeedOptions>();

        Assert.True(seed.Enabled);
    }
}
