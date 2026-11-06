using System.Text.RegularExpressions;
using FacturAR.Application.ApiKeys;

namespace FacturAR.UnitTests;

public sealed class ApiKeyGeneratorTests
{
    private static readonly Regex FormatoValor = new(
        "^fak_[0-9a-f]{12}_[A-Za-z0-9_-]{43}$",
        RegexOptions.CultureInvariant);

    private static readonly Regex FormatoPrefijo = new(
        "^[0-9a-f]{12}$",
        RegexOptions.CultureInvariant);

    private static readonly Regex FormatoSecreto = new(
        "^[A-Za-z0-9_-]{43}$",
        RegexOptions.CultureInvariant);

    [Fact]
    public void Generar_Prefijo_EsDoceHexadecimalesEnMinusculas()
    {
        var generado = new ApiKeyGenerator(new ApiKeysOptions()).Generar();

        Assert.Matches(FormatoPrefijo, generado.Prefix);
        Assert.Equal(12, generado.Prefix.Length);
    }

    [Fact]
    public void Generar_Secreto_EsCuarentaYTresCaracteresBase64UrlSinPadding()
    {
        var generado = new ApiKeyGenerator(new ApiKeysOptions()).Generar();

        Assert.Matches(FormatoSecreto, generado.Secret);
        Assert.Equal(43, generado.Secret.Length);
        Assert.DoesNotContain("=", generado.Secret);
    }

    [Fact]
    public void Generar_Valor_TieneElFormatoFakPrefijoSecreto()
    {
        var generado = new ApiKeyGenerator(new ApiKeysOptions()).Generar();

        Assert.Matches(FormatoValor, generado.Valor);
        Assert.Equal($"fak_{generado.Prefix}_{generado.Secret}", generado.Valor);
    }

    [Fact]
    public void Generar_DosLlamadas_ProducenValoresDistintos()
    {
        var generator = new ApiKeyGenerator(new ApiKeysOptions());

        var prefijos = new HashSet<string>();
        var secretos = new HashSet<string>();
        var valores = new HashSet<string>();

        for (var i = 0; i < 50; i++)
        {
            var generado = generator.Generar();
            prefijos.Add(generado.Prefix);
            secretos.Add(generado.Secret);
            valores.Add(generado.Valor);
        }

        Assert.Equal(50, prefijos.Count);
        Assert.Equal(50, secretos.Count);
        Assert.Equal(50, valores.Count);
    }
}
