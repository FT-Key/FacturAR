using FacturAR.Infrastructure.Outbox;

namespace FacturAR.UnitTests;

public sealed class OutboxBackoffTests
{
    private static OutboxBackoff SinJitter()
        => new(() => 0);

    [Theory]
    [InlineData(1, 5)]
    [InlineData(2, 10)]
    [InlineData(3, 20)]
    [InlineData(4, 40)]
    [InlineData(5, 80)]
    public void Calcular_ConBaseCinco_CreceExponencialmente(int intentos, double segundosEsperados)
    {
        var backoff = SinJitter();

        var espera = backoff.Calcular(intentos, 5, 0.2);

        Assert.Equal(segundosEsperados, espera.TotalSeconds, 3);
    }

    [Fact]
    public void Calcular_ConIntentosCero_UsaElIntervaloBase()
    {
        var espera = SinJitter().Calcular(0, 7, 0.2);

        Assert.Equal(7, espera.TotalSeconds, 3);
    }

    [Fact]
    public void Calcular_ConJitterAlMinimo_EsElValorBase()
    {
        var backoff = new OutboxBackoff(() => 0);

        var espera = backoff.Calcular(3, 5, 0.2);

        Assert.Equal(20, espera.TotalSeconds, 3);
    }

    [Fact]
    public void Calcular_ConJitterAlMaximo_QuedaVeintePorCientoArriba()
    {
        var backoff = new OutboxBackoff(() => 1);

        var espera = backoff.Calcular(3, 5, 0.2);

        Assert.Equal(24, espera.TotalSeconds, 3);
    }

    [Fact]
    public void Calcular_ConJitterDeshabilitado_EsExacto()
    {
        var backoff = new OutboxBackoff(() => 0);

        var espera = backoff.Calcular(4, 5, 0);

        Assert.Equal(40, espera.TotalSeconds, 3);
    }

    [Fact]
    public void Calcular_ConJitterAleatorio_QuedaDentroDelRangoPermitido()
    {
        var backoff = new OutboxBackoff(Random.Shared.NextDouble);

        for (var intentos = 1; intentos <= 5; intentos++)
        {
            var espera = backoff.Calcular(intentos, 5, 0.2);
            var esperado = 5 * Math.Pow(2, intentos - 1);

            Assert.InRange(espera.TotalSeconds, esperado, esperado * 1.2);
        }
    }
}
