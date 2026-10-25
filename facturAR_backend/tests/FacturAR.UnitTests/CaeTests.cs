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

    [Fact]
    public void CalcularVencimiento_SumaDiezDias()
    {
        var emision = new DateTime(2026, 10, 25, 18, 30, 0, DateTimeKind.Utc);

        var vencimiento = CaeGenerator.CalcularVencimiento(emision);

        Assert.Equal(new DateTime(2026, 11, 4), vencimiento);
    }
}
