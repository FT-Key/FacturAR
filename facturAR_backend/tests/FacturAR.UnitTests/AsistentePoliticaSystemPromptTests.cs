using FacturAR.Web.Services.Asistente;

namespace FacturAR.UnitTests;

public sealed class AsistentePoliticaSystemPromptTests
{
    [Theory]
    [InlineData("No existe registro libre ni auto-registro")]
    [InlineData("La aprobación es manual")]
    [InlineData("no crea la cuenta ni la empresa")]
    [InlineData("Aprobar la solicitud tampoco crea el usuario")]
    [InlineData("una invitación por email")]
    [InlineData("en /activar y define su contraseña")]
    [InlineData("La API key no se recibe por email ni se genera sola")]
    [InlineData("un usuario con rol Administrador la crea desde /api-keys")]
    [InlineData("El valor completo se muestra una sola vez")]
    [InlineData("primero se solicita el alta y que la cuenta se crea en la activación")]
    public void SystemPrompt_ContieneElContextoVerificadoDelProducto(string hecho)
    {
        Assert.Contains(hecho, AsistentePolitica.SystemPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public void SystemPrompt_OrdenDelProceso_EsSolicitudAprobacionActivacionApiKeyEmision()
    {
        var prompt = AsistentePolitica.SystemPrompt;

        var solicitud = prompt.IndexOf("solicitud, revisión y aprobación", StringComparison.Ordinal);
        var activacion = prompt.IndexOf("activación en /activar", StringComparison.Ordinal);
        var apiKey = prompt.IndexOf("creación de la API key", StringComparison.Ordinal);
        var emision = prompt.IndexOf("y emisión", StringComparison.Ordinal);

        Assert.True(solicitud >= 0);
        Assert.True(solicitud < activacion);
        Assert.True(activacion < apiKey);
        Assert.True(apiKey < emision);
    }

    [Fact]
    public void SystemPrompt_ProhibeAfirmarApiKeyAlAprobarYRegistroLibre()
    {
        Assert.Contains(
            "No afirmes que la API key se recibe al aprobar ni que el registro es libre",
            AsistentePolitica.SystemPrompt,
            StringComparison.Ordinal);
    }
}
