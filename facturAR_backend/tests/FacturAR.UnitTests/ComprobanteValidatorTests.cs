using FacturAR.Domain.Entities;
using FacturAR.Domain.Enums;
using FacturAR.Domain.Exceptions;
using FacturAR.Domain.Validation;

namespace FacturAR.UnitTests;

public sealed class ComprobanteValidatorTests
{
    private const string CuitValido = "20123456786";
    private const string OtroCuitValido = "30712345671";
    private const string MensajeTipoNoHabilitado = "El tipo de comprobante no está habilitado en este endpoint.";
    private const string MensajeCondicionInadmisible =
        "La condición frente al IVA del receptor no es admisible para este tipo de comprobante.";

    private static readonly decimal[] Alicuotas = [0m, 5m, 10.5m, 21m, 27m];

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(14)]
    [InlineData(99)]
    public void ValidarTipoHabilitado_TipoNoHabilitado_LanzaComprobanteValidationException(int tipo)
    {
        var ex = Assert.Throws<ComprobanteValidationException>(
            () => ComprobanteValidator.ValidarTipoHabilitado((TipoComprobante)tipo));

        Assert.Equal(MensajeTipoNoHabilitado, ex.Message);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(6)]
    [InlineData(11)]
    public void ValidarTipoHabilitado_FacturasDelAlcance_NoLanza(int tipo)
        => ComprobanteValidator.ValidarTipoHabilitado((TipoComprobante)tipo);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ValidarPuntoVenta_CeroONegativo_LanzaComprobanteValidationException(int puntoVenta)
    {
        var ex = Assert.Throws<ComprobanteValidationException>(
            () => ComprobanteValidator.ValidarPuntoVenta(puntoVenta));

        Assert.Equal("El punto de venta es obligatorio.", ex.Message);
    }

    [Fact]
    public void ValidarPuntoVenta_Positivo_NoLanza()
        => ComprobanteValidator.ValidarPuntoVenta(1);

    [Fact]
    public void ValidarEmisor_ConsumidorFinalConFacturaA_LanzaEmisionInvalidaException()
    {
        var emisor = Emisor(CondicionIva.ConsumidorFinal);

        var ex = Assert.Throws<EmisionInvalidaException>(
            () => ComprobanteValidator.ValidarEmisor(emisor, TipoComprobante.FacturaA));

        Assert.Equal("Un emisor consumidor final no puede emitir una factura A.", ex.Message);
    }

    [Theory]
    [InlineData(TipoComprobante.FacturaB)]
    [InlineData(TipoComprobante.FacturaC)]
    public void ValidarEmisor_ConsumidorFinalConFacturaBOC_NoLanza(TipoComprobante tipo)
        => ComprobanteValidator.ValidarEmisor(Emisor(CondicionIva.ConsumidorFinal), tipo);

    [Fact]
    public void ValidarEmisor_ResponsableInscriptoConFacturaA_NoLanza()
        => ComprobanteValidator.ValidarEmisor(Emisor(CondicionIva.ResponsableInscripto), TipoComprobante.FacturaA);

    [Fact]
    public void ValidarReceptor_FacturaAConDocumentoInvalido_LanzaCuitInvalidoException()
    {
        var ex = Assert.Throws<CuitInvalidoException>(() => ComprobanteValidator.ValidarReceptor(
            TipoComprobante.FacturaA,
            "CUIT",
            "123456789",
            "Cliente S.A.",
            (int)CondicionIva.ResponsableInscripto));

        Assert.Equal("El receptor de una factura A debe tener un CUIT válido.", ex.Message);
    }

    [Fact]
    public void ValidarReceptor_FacturaAConCondicionConsumidorFinal_LanzaComprobanteValidationException()
    {
        var ex = Assert.Throws<ComprobanteValidationException>(() => ComprobanteValidator.ValidarReceptor(
            TipoComprobante.FacturaA,
            "CUIT",
            CuitValido,
            "Cliente S.A.",
            (int)CondicionIva.ConsumidorFinal));

        Assert.Equal(MensajeCondicionInadmisible, ex.Message);
    }

    [Fact]
    public void ValidarReceptor_FacturaASinRazonSocial_LanzaComprobanteValidationException()
    {
        var ex = Assert.Throws<ComprobanteValidationException>(() => ComprobanteValidator.ValidarReceptor(
            TipoComprobante.FacturaA,
            "CUIT",
            CuitValido,
            "   ",
            (int)CondicionIva.ResponsableInscripto));

        Assert.Equal("La razón social del receptor es obligatoria.", ex.Message);
    }

    [Fact]
    public void ValidarReceptor_FacturaAValida_DevuelveElReceptorNormalizado()
    {
        var receptor = ComprobanteValidator.ValidarReceptor(
            TipoComprobante.FacturaA,
            "CUIT",
            "20-12345678-6",
            "  Cliente S.A.  ",
            (int)CondicionIva.ResponsableInscripto);

        Assert.Equal("CUIT", receptor.DocumentoTipo);
        Assert.Equal(CuitValido, receptor.DocumentoNumero);
        Assert.Equal("Cliente S.A.", receptor.RazonSocial);
        Assert.Equal(CondicionIva.ResponsableInscripto, receptor.CondicionIva);
    }

    [Fact]
    public void ValidarReceptor_FacturaBSinRazonSocial_LanzaComprobanteValidationException()
    {
        var ex = Assert.Throws<ComprobanteValidationException>(() => ComprobanteValidator.ValidarReceptor(
            TipoComprobante.FacturaB,
            "99",
            "30123456",
            string.Empty,
            (int)CondicionIva.ConsumidorFinal));

        Assert.Equal("La razón social del receptor es obligatoria.", ex.Message);
    }

    [Fact]
    public void ValidarReceptor_FacturaBConConsumidorFinalYDni_DevuelveElDocumento()
    {
        var receptor = ComprobanteValidator.ValidarReceptor(
            TipoComprobante.FacturaB,
            "99",
            "30123456",
            "Cliente S.A.",
            (int)CondicionIva.ConsumidorFinal);

        Assert.Equal("99", receptor.DocumentoTipo);
        Assert.Equal("30123456", receptor.DocumentoNumero);
        Assert.Equal(CondicionIva.ConsumidorFinal, receptor.CondicionIva);
    }

    [Fact]
    public void ValidarReceptor_FacturaBConTipoDni_DevuelveElDniNormalizado()
    {
        var receptor = ComprobanteValidator.ValidarReceptor(
            TipoComprobante.FacturaB,
            "DNI",
            "30-123-456",
            "Cliente S.A.",
            (int)CondicionIva.ConsumidorFinal);

        Assert.Equal("DNI", receptor.DocumentoTipo);
        Assert.Equal("30123456", receptor.DocumentoNumero);
        Assert.Equal(CondicionIva.ConsumidorFinal, receptor.CondicionIva);
    }

    [Fact]
    public void ValidarReceptor_FacturaBConTipoDniInvalido_LanzaCuitInvalidoException()
    {
        Assert.Throws<CuitInvalidoException>(() => ComprobanteValidator.ValidarReceptor(
            TipoComprobante.FacturaB,
            "DNI",
            "123",
            "Cliente S.A.",
            (int)CondicionIva.ConsumidorFinal));
    }

    [Fact]
    public void ValidarReceptor_FacturaBConConsumidorFinalYDocumentoInvalido_LanzaCuitInvalidoException()
    {
        var ex = Assert.Throws<CuitInvalidoException>(() => ComprobanteValidator.ValidarReceptor(
            TipoComprobante.FacturaB,
            "99",
            "123",
            "Cliente S.A.",
            (int)CondicionIva.ConsumidorFinal));

        Assert.Equal("El documento del receptor no es válido.", ex.Message);
    }

    [Fact]
    public void ValidarReceptor_FacturaBConConsumidorFinalYCuitValido_DevuelveElCuitNormalizado()
    {
        var receptor = ComprobanteValidator.ValidarReceptor(
            TipoComprobante.FacturaB,
            "99",
            "30-71234567-1",
            "Cliente S.A.",
            (int)CondicionIva.Monotributo);

        Assert.Equal(OtroCuitValido, receptor.DocumentoNumero);
        Assert.Equal(CondicionIva.Monotributo, receptor.CondicionIva);
    }

    [Fact]
    public void ValidarReceptor_FacturaBConCuitYTipoDistintoDeNoventaYNueve_DevuelveElReceptor()
    {
        var receptor = ComprobanteValidator.ValidarReceptor(
            TipoComprobante.FacturaB,
            "CUIT",
            OtroCuitValido,
            "Cliente S.A.",
            (int)CondicionIva.Exento);

        Assert.Equal("CUIT", receptor.DocumentoTipo);
        Assert.Equal(OtroCuitValido, receptor.DocumentoNumero);
        Assert.Equal(CondicionIva.Exento, receptor.CondicionIva);
    }

    [Fact]
    public void ValidarReceptor_FacturaBConCuitInvalido_LanzaCuitInvalidoException()
    {
        var ex = Assert.Throws<CuitInvalidoException>(() => ComprobanteValidator.ValidarReceptor(
            TipoComprobante.FacturaB,
            "CUIT",
            "123456789",
            "Cliente S.A.",
            (int)CondicionIva.Exento));

        Assert.Equal("El documento del receptor no es válido.", ex.Message);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    public void ValidarReceptor_FacturaBConCondicionNoAdmisible_LanzaComprobanteValidationException(int condicion)
    {
        var ex = Assert.Throws<ComprobanteValidationException>(() => ComprobanteValidator.ValidarReceptor(
            TipoComprobante.FacturaB,
            "99",
            "30123456",
            "Cliente S.A.",
            condicion));

        Assert.Equal(MensajeCondicionInadmisible, ex.Message);
    }

    [Fact]
    public void ValidarReceptor_FacturaBSinCondicionIva_LanzaComprobanteValidationException()
    {
        var ex = Assert.Throws<ComprobanteValidationException>(() => ComprobanteValidator.ValidarReceptor(
            TipoComprobante.FacturaB,
            "99",
            "30123456",
            "Cliente S.A.",
            null));

        Assert.Equal(MensajeCondicionInadmisible, ex.Message);
    }

    [Fact]
    public void ValidarReceptor_FacturaCConConsumidorFinalSinNumero_DevuelveElReceptor()
    {
        var receptor = ComprobanteValidator.ValidarReceptor(
            TipoComprobante.FacturaC,
            "99",
            null,
            null,
            (int)CondicionIva.ConsumidorFinal);

        Assert.Equal("99", receptor.DocumentoTipo);
        Assert.Equal(string.Empty, receptor.DocumentoNumero);
        Assert.Equal(string.Empty, receptor.RazonSocial);
        Assert.Equal(CondicionIva.ConsumidorFinal, receptor.CondicionIva);
    }

    [Fact]
    public void ValidarReceptor_FacturaCConCuitValido_DevuelveElCuitNormalizado()
    {
        var receptor = ComprobanteValidator.ValidarReceptor(
            TipoComprobante.FacturaC,
            "cuit",
            "20-12345678-6",
            "Cliente S.A.",
            (int)CondicionIva.ConsumidorFinal);

        Assert.Equal(CuitValido, receptor.DocumentoNumero);
    }

    [Fact]
    public void ValidarReceptor_FacturaCConCuitInvalido_LanzaCuitInvalidoException()
    {
        var ex = Assert.Throws<CuitInvalidoException>(() => ComprobanteValidator.ValidarReceptor(
            TipoComprobante.FacturaC,
            "CUIT",
            "123456789",
            "Cliente S.A.",
            (int)CondicionIva.ConsumidorFinal));

        Assert.Equal("El documento del receptor no es válido.", ex.Message);
    }

    [Fact]
    public void ValidarReceptor_FacturaCConOtroTipoDeDocumento_LanzaComprobanteValidationException()
    {
        var ex = Assert.Throws<ComprobanteValidationException>(() => ComprobanteValidator.ValidarReceptor(
            TipoComprobante.FacturaC,
            "DNI",
            "30123456",
            "Cliente S.A.",
            (int)CondicionIva.ConsumidorFinal));

        Assert.Equal("La factura C se emite a consumidor final.", ex.Message);
    }

    [Fact]
    public void ValidarReceptor_FacturaCConCondicionDistintaDeConsumidorFinal_LanzaComprobanteValidationException()
    {
        var ex = Assert.Throws<ComprobanteValidationException>(() => ComprobanteValidator.ValidarReceptor(
            TipoComprobante.FacturaC,
            "99",
            string.Empty,
            string.Empty,
            (int)CondicionIva.Monotributo));

        Assert.Equal(MensajeCondicionInadmisible, ex.Message);
    }

    [Fact]
    public void ValidarItems_SinItems_LanzaComprobanteValidationException()
    {
        var ex = Assert.Throws<ComprobanteValidationException>(
            () => ComprobanteValidator.ValidarItems([], Alicuotas));

        Assert.Equal("El comprobante debe tener entre 1 y 500 ítems.", ex.Message);
    }

    [Fact]
    public void ValidarItems_ConMasDeQuinientosItems_LanzaComprobanteValidationException()
    {
        var items = Enumerable.Range(0, 501).Select(_ => Item()).ToList();

        var ex = Assert.Throws<ComprobanteValidationException>(
            () => ComprobanteValidator.ValidarItems(items, Alicuotas));

        Assert.Equal("El comprobante debe tener entre 1 y 500 ítems.", ex.Message);
    }

    [Fact]
    public void ValidarItems_ConQuinientosItems_NoLanza()
    {
        var items = Enumerable.Range(0, 500).Select(_ => Item()).ToList();

        ComprobanteValidator.ValidarItems(items, Alicuotas);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidarItems_DescripcionVacia_LanzaComprobanteValidationException(string descripcion)
    {
        var ex = Assert.Throws<ComprobanteValidationException>(
            () => ComprobanteValidator.ValidarItems([Item(descripcion: descripcion)], Alicuotas));

        Assert.Equal("Cada ítem debe tener descripción de 1 a 500 caracteres.", ex.Message);
    }

    [Fact]
    public void ValidarItems_DescripcionDeQuinientosUnCaracteres_LanzaComprobanteValidationException()
    {
        var ex = Assert.Throws<ComprobanteValidationException>(
            () => ComprobanteValidator.ValidarItems([Item(descripcion: new string('a', 501))], Alicuotas));

        Assert.Equal("Cada ítem debe tener descripción de 1 a 500 caracteres.", ex.Message);
    }

    [Fact]
    public void ValidarItems_DescripcionDeQuinientosCaracteres_NoLanza()
        => ComprobanteValidator.ValidarItems([Item(descripcion: new string('a', 500))], Alicuotas);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ValidarItems_CantidadNoPositiva_LanzaComprobanteValidationException(decimal cantidad)
    {
        var ex = Assert.Throws<ComprobanteValidationException>(
            () => ComprobanteValidator.ValidarItems([Item(cantidad: cantidad)], Alicuotas));

        Assert.Equal("La cantidad de cada ítem debe ser mayor que cero.", ex.Message);
    }

    [Fact]
    public void ValidarItems_PrecioNegativo_LanzaComprobanteValidationException()
    {
        var ex = Assert.Throws<ComprobanteValidationException>(
            () => ComprobanteValidator.ValidarItems([Item(precioUnitario: -1m)], Alicuotas));

        Assert.Equal("El precio unitario no puede ser negativo.", ex.Message);
    }

    [Fact]
    public void ValidarItems_PrecioCero_NoLanza()
        => ComprobanteValidator.ValidarItems([Item(precioUnitario: 0m)], Alicuotas);

    [Fact]
    public void ValidarItems_AlicuotaNoPermitida_LanzaComprobanteValidationException()
    {
        var ex = Assert.Throws<ComprobanteValidationException>(
            () => ComprobanteValidator.ValidarItems([Item(alicuotaIva: 12m)], Alicuotas));

        Assert.Equal("La alícuota de IVA no está permitida.", ex.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(10.5)]
    [InlineData(21)]
    [InlineData(27)]
    public void ValidarItems_AlicuotaPermitida_NoLanza(decimal alicuotaIva)
        => ComprobanteValidator.ValidarItems([Item(alicuotaIva: alicuotaIva)], Alicuotas);

    [Fact]
    public void ValidarObservaciones_SinObservaciones_NoLanza()
        => ComprobanteValidator.ValidarObservaciones(null);

    [Fact]
    public void ValidarObservaciones_ConQuinientosCaracteres_NoLanza()
        => ComprobanteValidator.ValidarObservaciones(new string('a', 500));

    [Fact]
    public void ValidarObservaciones_ConMasDeQuinientosCaracteres_LanzaComprobanteValidationException()
    {
        var ex = Assert.Throws<ComprobanteValidationException>(
            () => ComprobanteValidator.ValidarObservaciones(new string('a', 501)));

        Assert.Equal("Las observaciones admiten hasta 500 caracteres.", ex.Message);
    }

    [Fact]
    public void ValidarImporteTotal_SinImporteInformado_NoLanza()
        => ComprobanteValidator.ValidarImporteTotal(null, 12100m);

    [Fact]
    public void ValidarImporteTotal_ImporteCoincidente_NoLanza()
        => ComprobanteValidator.ValidarImporteTotal(12100m, 12100m);

    [Fact]
    public void ValidarImporteTotal_ImporteQueRedondeaAlCalculado_NoLanza()
        => ComprobanteValidator.ValidarImporteTotal(12100.004m, 12100m);

    [Fact]
    public void ValidarImporteTotal_ImporteQueRedondeaDistinto_LanzaComprobanteValidationException()
    {
        var ex = Assert.Throws<ComprobanteValidationException>(
            () => ComprobanteValidator.ValidarImporteTotal(12100.005m, 12100m));

        Assert.Equal("El importe total no coincide con el cálculo de los ítems.", ex.Message);
    }

    [Fact]
    public void ValidarImporteTotal_ImporteInconsistente_LanzaComprobanteValidationException()
    {
        var ex = Assert.Throws<ComprobanteValidationException>(
            () => ComprobanteValidator.ValidarImporteTotal(9999m, 12100m));

        Assert.Equal("El importe total no coincide con el cálculo de los ítems.", ex.Message);
    }

    private static Contribuyente Emisor(CondicionIva condicion)
        => new()
        {
            RazonSocial = "Demo SRL",
            Cuit = CuitValido,
            CondicionIva = condicion,
            Activo = true
        };

    private static ComprobanteItem Item(
        string descripcion = "Servicio",
        decimal cantidad = 1m,
        decimal precioUnitario = 100m,
        decimal alicuotaIva = 21m)
        => new()
        {
            Descripcion = descripcion,
            Cantidad = cantidad,
            PrecioUnitario = precioUnitario,
            AlicuotaIva = alicuotaIva
        };
}
