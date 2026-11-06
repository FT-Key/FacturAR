using FacturAR.Web.Services;

namespace FacturAR.UnitTests;

public sealed class EmisionIdempotenciaTests
{
    [Fact]
    public void GenerarClave_Devuelve32CaracteresHexadecimales()
    {
        var clave = EmisionIdempotencia.GenerarClave();

        Assert.Equal(32, clave.Length);
        Assert.All(clave, caracter => Assert.True(Uri.IsHexDigit(caracter)));
        Assert.True(Guid.TryParseExact(clave, "N", out _));
    }

    [Fact]
    public void DebeRegenerar_MismaFirma_DevuelveFalse()
    {
        Assert.False(EmisionIdempotencia.DebeRegenerar("misma-firma", "misma-firma"));
    }

    [Fact]
    public void DebeRegenerar_FirmaDistinta_DevuelveTrue()
    {
        Assert.True(EmisionIdempotencia.DebeRegenerar("firma-anterior", "firma-nueva"));
    }
}
