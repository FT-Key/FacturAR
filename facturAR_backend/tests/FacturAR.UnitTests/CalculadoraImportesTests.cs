using FacturAR.Application.Comprobantes;
using FacturAR.Domain.Entities;
using FacturAR.Domain.Enums;
using FacturAR.Domain.Exceptions;

namespace FacturAR.UnitTests;

public sealed class CalculadoraImportesTests
{
    [Fact]
    public void Calcular_FacturaADosPorCincoMil_DiscriminaIva()
    {
        var calculo = CalculadoraImportes.Calcular(
            [Item(2m, 5000m, 21m)],
            TipoComprobante.FacturaA);

        Assert.Equal(10000m, calculo.ImporteNeto);
        Assert.Equal(2100m, calculo.ImporteIva);
        Assert.Equal(12100m, calculo.ImporteTotal);
        Assert.Equal(10000m, Assert.Single(calculo.Items).Subtotal);
    }

    [Fact]
    public void Calcular_FacturaBUnoPorDoceMilCiento_NoDiscriminaIva()
    {
        var calculo = CalculadoraImportes.Calcular(
            [Item(1m, 12100m, 21m)],
            TipoComprobante.FacturaB);

        Assert.Equal(12100m, calculo.ImporteNeto);
        Assert.Equal(0m, calculo.ImporteIva);
        Assert.Equal(12100m, calculo.ImporteTotal);
    }

    [Fact]
    public void Calcular_FacturaCUnoPorDiezMil_NoDiscriminaIva()
    {
        var calculo = CalculadoraImportes.Calcular(
            [Item(1m, 10000m, 21m)],
            TipoComprobante.FacturaC);

        Assert.Equal(10000m, calculo.ImporteNeto);
        Assert.Equal(0m, calculo.ImporteIva);
        Assert.Equal(10000m, calculo.ImporteTotal);
    }

    [Fact]
    public void Calcular_FacturaAConAlicuotaCero_DevuelveIvaCero()
    {
        var calculo = CalculadoraImportes.Calcular(
            [Item(1m, 10000m, 0m)],
            TipoComprobante.FacturaA);

        Assert.Equal(10000m, calculo.ImporteNeto);
        Assert.Equal(0m, calculo.ImporteIva);
        Assert.Equal(10000m, calculo.ImporteTotal);
    }

    [Fact]
    public void Calcular_SubtotalConMitadExacta_RedondeaAwayFromZero()
    {
        var calculo = CalculadoraImportes.Calcular(
            [Item(1m, 10.005m, 0m)],
            TipoComprobante.FacturaC);

        Assert.Equal(10.01m, Assert.Single(calculo.Items).Subtotal);
        Assert.Equal(10.01m, calculo.ImporteNeto);
        Assert.Equal(10.01m, calculo.ImporteTotal);
    }

    [Fact]
    public void Calcular_VariosItems_SumaLosSubtotales()
    {
        var calculo = CalculadoraImportes.Calcular(
            [Item(2m, 1000m, 0m), Item(3m, 250m, 0m)],
            TipoComprobante.FacturaC);

        Assert.Equal(2, calculo.Items.Count);
        Assert.Equal(2000m, calculo.Items[0].Subtotal);
        Assert.Equal(750m, calculo.Items[1].Subtotal);
        Assert.Equal(2750m, calculo.ImporteNeto);
        Assert.Equal(0m, calculo.ImporteIva);
        Assert.Equal(2750m, calculo.ImporteTotal);
    }

    [Fact]
    public void Calcular_FacturaA_RedondeaElIvaPorItemAntesDeSumar()
    {
        var calculo = CalculadoraImportes.Calcular(
            [Item(1m, 0.05m, 27m), Item(1m, 0.05m, 27m)],
            TipoComprobante.FacturaA);

        Assert.Equal(0.10m, calculo.ImporteNeto);
        Assert.Equal(0.02m, calculo.ImporteIva);
        Assert.Equal(0.12m, calculo.ImporteTotal);
    }

    [Fact]
    public void Calcular_ImporteQueDesborda_LanzaComprobanteValidationException()
    {
        var ex = Assert.Throws<ComprobanteValidationException>(() => CalculadoraImportes.Calcular(
            [Item(decimal.MaxValue, 2m, 21m)],
            TipoComprobante.FacturaA));

        Assert.Equal("Los importes del comprobante no son válidos.", ex.Message);
    }

    private static ComprobanteItem Item(decimal cantidad, decimal precioUnitario, decimal alicuotaIva)
        => new()
        {
            Descripcion = "Servicio",
            Cantidad = cantidad,
            PrecioUnitario = precioUnitario,
            AlicuotaIva = alicuotaIva
        };
}
