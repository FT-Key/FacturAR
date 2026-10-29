using FacturAR.Application.Comprobantes;
using FacturAR.Domain.Validation;

namespace FacturAR.UnitTests;

public sealed class CaeTests
{
    [Fact]
    public void Generar_DevuelveCatorceDigitos()
    {
        for (var i = 0; i < 100; i++)
        {
            var cae = CaeGenerator.Generar();

            Assert.Equal(14, cae.Length);
            Assert.True(CaeValidator.IsValid(cae));
            Assert.NotEqual('0', cae[0]);
        }
    }

    [Fact]
    public void IsValid_RechazaCaeConLetras()
    {
        Assert.False(CaeValidator.IsValid("1234567890123A"));
    }

    [Theory]
    [InlineData("12345678901234", "12345678901234")]
    [InlineData(" 12345678901234 ", "12345678901234")]
    [InlineData("12-3456-78901234", "12345678901234")]
    [InlineData("12.3456.78901234", "12345678901234")]
    [InlineData("12 3456 78901234", "12345678901234")]
    [InlineData("1234567890123-", "1234567890123")]
    public void Normalize_QuitaEspaciosYSeparadores(string entrada, string esperado)
    {
        Assert.Equal(esperado, CaeValidator.Normalize(entrada));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_DevuelveVacioSiNoHayDigitos(string? entrada)
    {
        Assert.Equal(string.Empty, CaeValidator.Normalize(entrada));
    }

    [Fact]
    public void Normalize_NoEliminaLetras()
    {
        Assert.Equal("1234567890123A", CaeValidator.Normalize("1234567890123A"));
        Assert.False(CaeValidator.IsValid(CaeValidator.Normalize("1234567890123A")));
    }

    [Fact]
    public void CaeNormalizadoEsValido()
    {
        var normalizado = CaeValidator.Normalize("12-3456-78901234");

        Assert.True(CaeValidator.IsValid(normalizado));
    }

    [Fact]
    public void CalcularVencimiento_SumaDiezDias()
    {
        var emision = new DateTime(2026, 10, 25, 18, 30, 0, DateTimeKind.Utc);

        var vencimiento = CaeGenerator.CalcularVencimiento(emision);

        Assert.Equal(new DateTime(2026, 11, 4), vencimiento);
    }
}
