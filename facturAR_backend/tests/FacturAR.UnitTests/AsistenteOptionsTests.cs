using FacturAR.Web.Services.Asistente;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FacturAR.UnitTests;

public sealed class AsistenteOptionsTests
{
    private const string MensajeError =
        "El asistente está habilitado pero falta la clave de Groq. Configurá Asistente:GroqApiKey o la variable GROQ_API_KEY.";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validacion_HabilitadoSinClave_Falla(string? clave)
    {
        var ex = Assert.Throws<OptionsValidationException>(() => Validar(clave));

        Assert.Contains("GroqApiKey", ex.Message);
    }

    [Fact]
    public void Validacion_HabilitadoConClave_Pasa()
    {
        Validar("clave-de-prueba");
    }

    [Fact]
    public void Validacion_DeshabilitadoSinClave_Pasa()
    {
        Validar(null, habilitado: false);
    }

    private static void Validar(string? clave, bool habilitado = true)
    {
        var servicios = new ServiceCollection();
        servicios.AddOptions<AsistenteOptions>()
            .Configure(options =>
            {
                options.Habilitado = habilitado;
                options.GroqApiKey = clave;
            })
            .Validate(
                options => !options.Habilitado || !string.IsNullOrWhiteSpace(options.GroqApiKey),
                MensajeError);

        using var proveedor = servicios.BuildServiceProvider();
        _ = proveedor.GetRequiredService<IOptions<AsistenteOptions>>().Value;
    }
}
