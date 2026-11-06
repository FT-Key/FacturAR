using FacturAR.Application.Parametros;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FacturAR.IntegrationTests;

public sealed class ParametrosConfigurationTests
{
    [Fact]
    public void CP1801_SinConfiguracion_DevuelveLosValoresDelMvp()
    {
        var parametros = Cargar();

        Assert.Equal(10, parametros.DiasVencimientoCae);
        Assert.Equal(new decimal[] { 0m, 5m, 10.5m, 21m, 27m }, parametros.AlicuotasPermitidas);
        Assert.Equal(1, parametros.PuntoVentaPorDefecto);
        Assert.Equal("ARS", parametros.Moneda);
        Assert.Equal(20, parametros.PaginaPorDefecto);
        Assert.Equal(100, parametros.PaginaMaxima);
    }

    [Fact]
    public void CP1802_MonedaPorConfiguracion_GanaSobreElDefault()
    {
        var parametros = Cargar(("Facturacion:Moneda", "USD"));

        Assert.Equal("USD", parametros.Moneda);
    }

    [Fact]
    public void AlicuotasPermitidas_SeBindeanDesdeConfiguracion()
    {
        using var factory = new ConfiguredWebApplicationFactory(new Dictionary<string, string>
        {
            ["Facturacion:AlicuotasPermitidas:4"] = "30"
        });

        var parametros = factory.Services.GetRequiredService<ParametrosFacturacion>();

        Assert.Equal(new decimal[] { 0m, 5m, 10.5m, 21m, 30m }, parametros.AlicuotasPermitidas);
    }

    [Fact]
    public void ConfiguracionDeLaApi_ExponeElOverrideDeFacturacion()
    {
        using var factory = new ConfiguredWebApplicationFactory(new Dictionary<string, string>
        {
            ["Facturacion:Moneda"] = "USD"
        });

        var parametros = factory.Services.GetRequiredService<ParametrosFacturacion>();

        Assert.Equal("USD", parametros.Moneda);
        Assert.Equal(10, parametros.DiasVencimientoCae);
    }

    private static ParametrosFacturacion Cargar(params (string Clave, string Valor)[] valores)
    {
        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(valores.Select(v => new KeyValuePair<string, string?>(v.Clave, v.Valor)))
            .Build();

        return configuracion
            .GetSection(ParametrosFacturacion.SectionName)
            .Get<ParametrosFacturacion>() ?? new ParametrosFacturacion();
    }
}
