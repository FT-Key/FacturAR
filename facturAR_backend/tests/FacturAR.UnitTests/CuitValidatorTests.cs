using FacturAR.Domain.Validation;

namespace FacturAR.UnitTests;

public sealed class CuitValidatorTests
{
    [Theory]
    [InlineData("20123456786")]
    [InlineData("20-12345678-6")]
    [InlineData("30712345671")]
    public void IsValid_ConCuitValido_RetornaTrue(string cuit)
    {
        Assert.True(CuitValidator.IsValid(cuit));
    }

    [Theory]
    [InlineData("20123456780")]
    [InlineData("2012345678")]
    [InlineData("2012345678A")]
    [InlineData("")]
    [InlineData(null)]
    public void IsValid_ConCuitInvalido_RetornaFalse(string? cuit)
    {
        Assert.False(CuitValidator.IsValid(cuit));
    }

    [Fact]
    public void Normalize_EliminaSeparadores()
    {
        Assert.Equal("20123456786", CuitValidator.Normalize("20-12345678-6"));
    }

    [Fact]
    public void TryNormalize_ConCuitValido_DevuelveNormalizado()
    {
        var resultado = CuitValidator.TryNormalize("20-12345678-6", out var normalizado);

        Assert.True(resultado);
        Assert.Equal("20123456786", normalizado);
    }
}
