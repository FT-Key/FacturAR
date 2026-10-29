using FacturAR.Application.Notificaciones;
using FacturAR.Infrastructure.Notificaciones;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FacturAR.IntegrationTests;

public sealed class EmailSenderProviderTests
{
    [Fact]
    public void ConProviderFileEnDevelopment_RegistraFileEmailSender()
    {
        using var factory = new EmailProviderWebApplicationFactory(
            environment: "Development",
            settings: new Dictionary<string, string?>
            {
                ["Email:Provider"] = "File"
            });

        var sender = factory.Services.GetRequiredService<IEmailSender>();

        Assert.IsType<FileEmailSender>(sender);
    }

    [Fact]
    public void ConProviderSmtp_RegistraSmtpEmailSenderSinRecompilar()
    {
        using var factory = new EmailProviderWebApplicationFactory(
            environment: "Production",
            settings: new Dictionary<string, string?>
            {
                ["Email:Provider"] = "Smtp",
                ["Email:Host"] = "smtp.invalid",
                ["Email:From"] = "facturar@dominio.com",
                ["Portal:BaseUrl"] = "https://portal.dominio.com"
            });

        var sender = factory.Services.GetRequiredService<IEmailSender>();

        Assert.IsType<SmtpEmailSender>(sender);
    }

    [Fact]
    public void ConProviderFileFueraDeDevelopment_LaAppFallaAlArrancar()
    {
        using var factory = new EmailProviderWebApplicationFactory(
            environment: "Production",
            settings: new Dictionary<string, string?>
            {
                ["Email:Provider"] = "File"
            });

        var excepcion = Record.Exception(() => factory.Services);

        Assert.NotNull(excepcion);
        Assert.Contains("Email:Provider=File", excepcion.ToString(), StringComparison.Ordinal);
        Assert.Contains("Development", excepcion.ToString(), StringComparison.Ordinal);
    }
}

internal sealed class EmailProviderWebApplicationFactory(
    string environment,
    Dictionary<string, string?> settings) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.UseSetting("Mongo:InitializeIndexes", "false");

        foreach (var (clave, valor) in settings)
        {
            builder.UseSetting(clave, valor);
        }
    }
}
