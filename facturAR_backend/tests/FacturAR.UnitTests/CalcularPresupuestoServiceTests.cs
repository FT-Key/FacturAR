using FacturAR.Application.Abstractions;
using FacturAR.Application.Comprobantes;
using FacturAR.Contracts.Comprobantes;
using FacturAR.Domain.Enums;
using FacturAR.Domain.Exceptions;

namespace FacturAR.UnitTests;

public sealed class CalcularPresupuestoServiceTests
{
    private static readonly Guid ContribuyenteId = Guid.NewGuid();

    [Fact]
    public async Task Calcular_FacturaADosPorMilConIva21_DevuelveNetoIvaTotalYSubtotal()
    {
        var servicio = CrearServicio();

        var presupuesto = await servicio.CalcularAsync(
            ContribuyenteId,
            FacturaA(importeTotal: null));

        Assert.Equal(2000m, presupuesto.ImporteNeto);
        Assert.Equal(420m, presupuesto.ImporteIva);
        Assert.Equal(2420m, presupuesto.ImporteTotal);

        var item = Assert.Single(presupuesto.Items);
        Assert.Equal("Servicio", item.Descripcion);
        Assert.Equal(2m, item.Cantidad);
        Assert.Equal(1000m, item.PrecioUnitario);
        Assert.Equal(21m, item.AlicuotaIva);
        Assert.Equal(2000m, item.Subtotal);
    }

    [Fact]
    public async Task Calcular_FacturaCUnoPorDiezMilConIva21_NoDiscriminaIva()
    {
        var servicio = CrearServicio();

        var presupuesto = await servicio.CalcularAsync(
            ContribuyenteId,
            FacturaC(cantidad: 1m, precioUnitario: 10000m, alicuotaIva: 21m));

        Assert.Equal(10000m, presupuesto.ImporteNeto);
        Assert.Equal(0m, presupuesto.ImporteIva);
        Assert.Equal(10000m, presupuesto.ImporteTotal);
        Assert.Equal(10000m, Assert.Single(presupuesto.Items).Subtotal);
    }

    [Fact]
    public async Task Calcular_FacturaBUnoPorMilConIva21_NoDiscriminaIva()
    {
        var servicio = CrearServicio();

        var presupuesto = await servicio.CalcularAsync(
            ContribuyenteId,
            FacturaB(cantidad: 1m, precioUnitario: 1000m, alicuotaIva: 21m));

        Assert.Equal(1000m, presupuesto.ImporteNeto);
        Assert.Equal(0m, presupuesto.ImporteIva);
        Assert.Equal(1000m, presupuesto.ImporteTotal);
        Assert.Equal(1000m, Assert.Single(presupuesto.Items).Subtotal);
    }

    [Fact]
    public async Task Calcular_CantidadCero_LanzaComprobanteValidationException()
    {
        var servicio = CrearServicio();

        await Assert.ThrowsAsync<ComprobanteValidationException>(
            () => servicio.CalcularAsync(
                ContribuyenteId,
                FacturaC(cantidad: 0m, precioUnitario: 10000m, alicuotaIva: 21m)));
    }

    [Fact]
    public async Task Calcular_DescripcionVacia_LanzaComprobanteValidationException()
    {
        var servicio = CrearServicio();

        await Assert.ThrowsAsync<ComprobanteValidationException>(
            () => servicio.CalcularAsync(
                ContribuyenteId,
                FacturaC(descripcion: "   ")));
    }

    [Fact]
    public async Task Calcular_AlicuotaNoPermitida_LanzaComprobanteValidationException()
    {
        var servicio = CrearServicio();

        await Assert.ThrowsAsync<ComprobanteValidationException>(
            () => servicio.CalcularAsync(
                ContribuyenteId,
                FacturaC(alicuotaIva: 19m)));
    }

    [Fact]
    public async Task Calcular_ReceptorInvalidoParaElTipo_LanzaComprobanteValidationException()
    {
        var servicio = CrearServicio();

        await Assert.ThrowsAsync<ComprobanteValidationException>(
            () => servicio.CalcularAsync(
                ContribuyenteId,
                FacturaA(condicionIva: (int)CondicionIva.ConsumidorFinal)));
    }

    [Fact]
    public async Task Calcular_ImporteTotalDistintoDelCalculado_LanzaComprobanteValidationException()
    {
        var servicio = CrearServicio();

        await Assert.ThrowsAsync<ComprobanteValidationException>(
            () => servicio.CalcularAsync(
                ContribuyenteId,
                FacturaA(importeTotal: 24200m)));
    }

    [Fact]
    public void Constructor_SoloDependeDeParametrosResolver()
    {
        var parametros = typeof(CalcularPresupuestoService)
            .GetConstructors()
            .SelectMany(ctor => ctor.GetParameters())
            .Select(parametro => parametro.ParameterType)
            .ToArray();

        Assert.Single(parametros);
        Assert.Equal(typeof(IParametrosResolver), parametros[0]);
    }

    private static CalcularPresupuestoService CrearServicio()
        => new(new ComprobanteParametrosFake());

    private static EmitirComprobanteRequest FacturaA(
        decimal? importeTotal = null,
        int condicionIva = (int)CondicionIva.ResponsableInscripto)
        => new(
            (int)TipoComprobante.FacturaA,
            1,
            null,
            new ReceptorComprobanteRequest("CUIT", "30712345671", "Cliente S.A.", condicionIva),
            [new ItemComprobanteRequest("Servicio", 2m, 1000m, 21m)],
            importeTotal,
            null);

    private static EmitirComprobanteRequest FacturaB(
        decimal cantidad = 1m,
        decimal precioUnitario = 1000m,
        decimal alicuotaIva = 21m)
        => new(
            (int)TipoComprobante.FacturaB,
            1,
            null,
            new ReceptorComprobanteRequest(
                "99",
                "30123456",
                "Cliente S.A.",
                (int)CondicionIva.ConsumidorFinal),
            [new ItemComprobanteRequest("Producto", cantidad, precioUnitario, alicuotaIva)],
            null,
            null);

    private static EmitirComprobanteRequest FacturaC(
        decimal cantidad = 1m,
        decimal precioUnitario = 10000m,
        decimal alicuotaIva = 21m,
        string descripcion = "Producto")
        => new(
            (int)TipoComprobante.FacturaC,
            1,
            null,
            new ReceptorComprobanteRequest(
                "99",
                "12345678",
                "Consumidor Final",
                (int)CondicionIva.ConsumidorFinal),
            [new ItemComprobanteRequest(descripcion, cantidad, precioUnitario, alicuotaIva)],
            null,
            null);
}
