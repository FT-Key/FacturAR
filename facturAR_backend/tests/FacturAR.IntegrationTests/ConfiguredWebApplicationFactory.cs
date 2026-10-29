using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FacturAR.IntegrationTests;

public sealed class ConfiguredWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly Dictionary<string, string> _settings;

    public ConfiguredWebApplicationFactory(Dictionary<string, string> settings)
    {
        _settings = settings;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Mongo:InitializeIndexes", "false");

        foreach (var setting in _settings)
        {
            builder.UseSetting(setting.Key, setting.Value);
        }
    }
}
