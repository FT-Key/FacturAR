using FacturAR.Contracts.Comprobantes;
using FacturAR.Domain.Entities;
using FacturAR.Domain.Enums;
using FacturAR.Domain.Exceptions;
using FacturAR.Domain.Validation;

namespace FacturAR.UnitTests;

public sealed class NotasCreditoDebitoTests
{
    [Theory]
    [InlineData(TipoComprobante.NotaDebitoA, TipoComprobante.FacturaA)]
    [InlineData(TipoComprobante.NotaCreditoA, TipoComprobante.FacturaA)]
    [InlineData(TipoComprobante.NotaDebitoB, TipoComprobante.FacturaB)]
    [InlineData(TipoComprobante.NotaCreditoB, TipoComprobante.FacturaB)]
    [InlineData(TipoComprobante.NotaDebitoC, TipoComprobante.FacturaC)]
    [InlineData(TipoComprobante.NotaCreditoC, TipoComprobante.FacturaC)]
    [InlineData(TipoComprobante.FacturaA, TipoComprobante.FacturaA)]
    public void TipoBase_DevuelveLaFacturaDeLaMismaLetra(TipoComprobante tipo, TipoComprobante esperado)
    {
        Assert.Equal(esperado, NotaComprobanteValidator.TipoBase(tipo));
    }

    [Fact]
    public void ValidarReferencia_ExigeOriginalEnNotasYLoProhibeEnFacturas()
    {
        var sinOriginal = Assert.Throws<ComprobanteValidationException>(
            () => NotaComprobanteValidator.ValidarReferencia(TipoComprobante.NotaCreditoA, null));
        var vacio = Assert.Throws<ComprobanteValidationException>(
            () => NotaComprobanteValidator.ValidarReferencia(TipoComprobante.NotaCreditoA, Guid.Empty));
        var enFactura = Assert.Throws<ComprobanteValidationException>(
            () => NotaComprobanteValidator.ValidarReferencia(TipoComprobante.FacturaA, Guid.NewGuid()));

        Assert.Equal("Falta el comprobante original de la nota.", sinOriginal.Message);
        Assert.Equal("Falta el comprobante original de la nota.", vacio.Message);
        Assert.Equal("Una factura no admite comprobante original.", enFactura.Message);
        NotaComprobanteValidator.ValidarReferencia(TipoComprobante.FacturaA, null);
        NotaComprobanteValidator.ValidarReferencia(TipoComprobante.NotaDebitoB, Guid.NewGuid());
    }

    [Theory]
    [InlineData(1000, 1000, 0)]
    [InlineData(12100, 1210, 10890)]
    public void CalcularNuevoSaldo_NotaDeCredito_DescuentaHastaElSaldo(decimal saldo, decimal importe, decimal esperado)
    {
        Assert.Equal(esperado, NotaComprobanteValidator.CalcularNuevoSaldo(TipoComprobante.NotaCreditoA, saldo, importe));
    }

    [Fact]
    public void CalcularNuevoSaldo_NotaDeCreditoSuperiorOSinSaldo_LanzaImporteExcedido()
    {
        var excedido = Assert.Throws<ImporteExcedidoException>(
            () => NotaComprobanteValidator.CalcularNuevoSaldo(TipoComprobante.NotaCreditoB, 1000m, 1500m));
        var sinSaldo = Assert.Throws<ImporteExcedidoException>(
            () => NotaComprobanteValidator.CalcularNuevoSaldo(TipoComprobante.NotaCreditoC, 0m, 1m));

        Assert.Equal(NotaComprobanteValidator.MensajeImporteExcedido, excedido.Message);
        Assert.Equal(NotaComprobanteValidator.MensajeSinSaldo, sinSaldo.Message);
    }

    [Fact]
    public void CalcularNuevoSaldo_NotaDeDebito_SumaSinLimite()
    {
        Assert.Equal(51000m, NotaComprobanteValidator.CalcularNuevoSaldo(TipoComprobante.NotaDebitoA, 1000m, 50000m));
        Assert.Equal(500m, NotaComprobanteValidator.CalcularNuevoSaldo(TipoComprobante.NotaDebitoC, 0m, 500m));
    }

    [Fact]
    public async Task CP0801_NotaDeCreditoA_TieneCaePropioYDejaElSaldoEn10890()
    {
        var ctx = new ComprobanteContexto();
        var factura = await EmitirFacturaAsync(ctx, ctx.FacturaA());
        ctx.Secuencias.Numero = 1;

        var nota = (await ctx.Servicio.EmitirAsync(ctx.Emisor.Id, "Emisor", NotaCreditoA(ctx, factura.Id), "idem-nc")).Comprobante;

        Assert.Equal((int)TipoComprobante.NotaCreditoA, nota.TipoComprobante);
        Assert.Equal("Nota de Crédito A", nota.TipoComprobanteDescripcion);
        Assert.Equal(1, nota.Numero);
        Assert.Matches("^[1-9][0-9]{13}$", nota.Cae);
        Assert.NotEqual(factura.Cae, nota.Cae);
        Assert.Equal(1000m, nota.ImporteNeto);
        Assert.Equal(210m, nota.ImporteIva);
        Assert.Equal(1210m, nota.ImporteTotal);
        Assert.Equal("Emitido", nota.Estado);
        Assert.Equal((ctx.Emisor.Id, 1, TipoComprobante.NotaCreditoA), ctx.Secuencias.UltimaClave);

        var original = ctx.Comprobantes.Comprobantes.Single(c => c.Id == factura.Id);
        Assert.Equal(10890m, original.ImporteOriginalAjustado);
        Assert.Equal(12100m, original.ImporteTotal);
        Assert.Equal(factura.Numero, original.Numero);
        Assert.Equal(factura.Cae, original.Cae);

        var guardada = ctx.Comprobantes.Comprobantes.Single(c => c.Id == nota.Id);
        Assert.Equal(factura.Id, guardada.ComprobanteOriginalId);
        Assert.Null(guardada.ImporteOriginalAjustado);

        var evento = Assert.Single(ctx.Eventos.Eventos, e => e.EntidadId == nota.Id.ToString());
        Assert.Equal("NotaEmitida", evento.Accion);
        Assert.Contains(factura.Id.ToString(), evento.Datos);
    }

    [Fact]
    public async Task Factura_GuardaSuSaldoInicialIgualAlTotal()
    {
        var ctx = new ComprobanteContexto();

        var factura = await EmitirFacturaAsync(ctx, ctx.FacturaA());

        var guardada = ctx.Comprobantes.Comprobantes.Single(c => c.Id == factura.Id);
        Assert.Equal(12100m, guardada.ImporteOriginalAjustado);
        Assert.Null(guardada.ComprobanteOriginalId);
    }

    [Fact]
    public async Task CP0802_CP0803_NotasDeCreditoByC_AjustanElSaldo()
    {
        var ctx = new ComprobanteContexto();
        var facturaB = await EmitirFacturaAsync(ctx, ctx.FacturaB());
        var facturaC = await EmitirFacturaAsync(ctx, ctx.FacturaC());

        var notaB = (await ctx.Servicio.EmitirAsync(ctx.Emisor.Id, null, NotaSobre(ctx.FacturaB(), TipoComprobante.NotaCreditoB, facturaB.Id, 1210m), "idem-ncb")).Comprobante;
        var notaC = (await ctx.Servicio.EmitirAsync(ctx.Emisor.Id, null, NotaSobre(ctx.FacturaC(), TipoComprobante.NotaCreditoC, facturaC.Id, 1000m), "idem-ncc")).Comprobante;

        Assert.Equal(0m, notaB.ImporteIva);
        Assert.Equal(1210m, notaB.ImporteTotal);
        Assert.Equal(1000m, notaC.ImporteTotal);
        Assert.Equal(10890m, Saldo(ctx, facturaB.Id));
        Assert.Equal(9000m, Saldo(ctx, facturaC.Id));
    }

    [Fact]
    public async Task CP0804_NotaASobreFacturaC_LanzaTipoIncompatibleSinConsumirNumero()
    {
        var ctx = new ComprobanteContexto();
        var facturaC = await EmitirFacturaAsync(ctx, ctx.FacturaC());
        var llamadas = ctx.Secuencias.Llamadas;

        var ex = await Assert.ThrowsAsync<TipoNotaIncompatibleException>(
            () => ctx.Servicio.EmitirAsync(ctx.Emisor.Id, null, NotaCreditoA(ctx, facturaC.Id), "idem-incompatible"));

        Assert.Equal(NotaComprobanteValidator.MensajeTipoIncompatible, ex.Message);
        Assert.Equal(llamadas, ctx.Secuencias.Llamadas);
        Assert.Equal(10000m, Saldo(ctx, facturaC.Id));
    }

    [Fact]
    public async Task CP0805_FacturaDeOtraEmpresa_LanzaNoEncontradoSinTocarSuSaldo()
    {
        var ctx = new ComprobanteContexto();
        var ajena = new Comprobante
        {
            ContribuyenteId = Guid.NewGuid(),
            TipoComprobante = TipoComprobante.FacturaA,
            Estado = EstadoComprobante.Emitido,
            ImporteTotal = 12100m,
            ImporteOriginalAjustado = 12100m
        };
        ctx.Comprobantes.Comprobantes.Add(ajena);

        var ex = await Assert.ThrowsAsync<ComprobanteNoEncontradoException>(
            () => ctx.Servicio.EmitirAsync(ctx.Emisor.Id, null, NotaCreditoA(ctx, ajena.Id), "idem-ajena"));

        Assert.Equal(NotaComprobanteValidator.MensajeOriginalNoEncontrado, ex.Message);
        Assert.Equal(12100m, ajena.ImporteOriginalAjustado);
        Assert.Equal(0, ctx.Secuencias.Llamadas);
    }

    [Theory]
    [InlineData(EstadoComprobante.Anulado, NotaComprobanteValidator.MensajeOriginalAnulado)]
    [InlineData(EstadoComprobante.Rechazado, NotaComprobanteValidator.MensajeOriginalAnulado)]
    [InlineData(EstadoComprobante.Borrador, NotaComprobanteValidator.MensajeOriginalNoAutorizado)]
    public async Task CP0806_CP0807_OriginalNoEmitido_LanzaComprobanteAnulado(EstadoComprobante estado, string mensaje)
    {
        var ctx = new ComprobanteContexto();
        var factura = await EmitirFacturaAsync(ctx, ctx.FacturaA());
        ctx.Comprobantes.Comprobantes.Single(c => c.Id == factura.Id).Estado = estado;
        var llamadas = ctx.Secuencias.Llamadas;

        var ex = await Assert.ThrowsAsync<ComprobanteAnuladoException>(
            () => ctx.Servicio.EmitirAsync(ctx.Emisor.Id, null, NotaCreditoA(ctx, factura.Id), "idem-anulado"));

        Assert.Equal(mensaje, ex.Message);
        Assert.Equal(llamadas, ctx.Secuencias.Llamadas);
    }

    [Fact]
    public async Task CP0808_CP0819_CreditoSuperiorAlSaldo_LanzaImporteExcedidoSinConsumirNumeroNiTransaccion()
    {
        var ctx = new ComprobanteContexto();
        var factura = await EmitirFacturaAsync(ctx, ctx.FacturaA());
        ctx.Comprobantes.Comprobantes.Single(c => c.Id == factura.Id).ImporteOriginalAjustado = 1000m;
        var llamadas = ctx.Secuencias.Llamadas;
        var transacciones = ctx.Transaccion.Ejecuciones;

        await Assert.ThrowsAsync<ImporteExcedidoException>(
            () => ctx.Servicio.EmitirAsync(ctx.Emisor.Id, null, NotaSobre(ctx.FacturaA(), TipoComprobante.NotaCreditoA, factura.Id, 1500m, 1500m, 0m), "idem-excedido"));

        Assert.Equal(llamadas, ctx.Secuencias.Llamadas);
        Assert.Equal(transacciones, ctx.Transaccion.Ejecuciones);
        Assert.Equal(1000m, Saldo(ctx, factura.Id));
        Assert.DoesNotContain(ctx.Comprobantes.Comprobantes, c => c.ComprobanteOriginalId == factura.Id);
    }

    [Fact]
    public async Task CP0809_CreditoPorElSaldoExacto_LoDejaEnCeroYUnaSegundaFalla()
    {
        var ctx = new ComprobanteContexto();
        var factura = await EmitirFacturaAsync(ctx, ctx.FacturaA());
        ctx.Comprobantes.Comprobantes.Single(c => c.Id == factura.Id).ImporteOriginalAjustado = 1000m;

        await ctx.Servicio.EmitirAsync(ctx.Emisor.Id, null, NotaSobre(ctx.FacturaA(), TipoComprobante.NotaCreditoA, factura.Id, 1000m, 1000m, 0m), "idem-total");

        Assert.Equal(0m, Saldo(ctx, factura.Id));
        var ex = await Assert.ThrowsAsync<ImporteExcedidoException>(
            () => ctx.Servicio.EmitirAsync(ctx.Emisor.Id, null, NotaSobre(ctx.FacturaA(), TipoComprobante.NotaCreditoA, factura.Id, 1m, 1m, 0m), "idem-otra"));
        Assert.Equal(NotaComprobanteValidator.MensajeSinSaldo, ex.Message);
    }

    [Fact]
    public async Task CP0810_CP0818_NotaDeDebito_SumaAlSaldoSinLimite()
    {
        var ctx = new ComprobanteContexto();
        var factura = await EmitirFacturaAsync(ctx, ctx.FacturaA());

        await ctx.Servicio.EmitirAsync(ctx.Emisor.Id, null, NotaSobre(ctx.FacturaA(), TipoComprobante.NotaDebitoA, factura.Id, 500m, 500m, 0m), "idem-nd1");
        Assert.Equal(12600m, Saldo(ctx, factura.Id));

        ctx.Comprobantes.Comprobantes.Single(c => c.Id == factura.Id).ImporteOriginalAjustado = 1000m;
        var nota = (await ctx.Servicio.EmitirAsync(ctx.Emisor.Id, null, NotaSobre(ctx.FacturaA(), TipoComprobante.NotaDebitoA, factura.Id, 50000m, 50000m, 0m), "idem-nd2")).Comprobante;

        Assert.Equal("Nota de Débito A", nota.TipoComprobanteDescripcion);
        Assert.Equal(51000m, Saldo(ctx, factura.Id));
    }

    [Fact]
    public async Task CP0811_CP0812_ReferenciaFaltanteOSobrante_LanzaValidacion()
    {
        var ctx = new ComprobanteContexto();
        var factura = await EmitirFacturaAsync(ctx, ctx.FacturaA());

        await Assert.ThrowsAsync<ComprobanteValidationException>(
            () => ctx.Servicio.EmitirAsync(ctx.Emisor.Id, null, NotaCreditoA(ctx, factura.Id) with { ComprobanteOriginalId = null }, "idem-sin"));
        await Assert.ThrowsAsync<ComprobanteValidationException>(
            () => ctx.Servicio.EmitirAsync(ctx.Emisor.Id, null, ctx.FacturaA() with { ComprobanteOriginalId = factura.Id }, "idem-con"));

        Assert.Single(ctx.Comprobantes.Comprobantes);
    }

    [Fact]
    public async Task CP0813_NotaSobreOtraNota_LanzaTipoIncompatible()
    {
        var ctx = new ComprobanteContexto();
        var factura = await EmitirFacturaAsync(ctx, ctx.FacturaA());
        var nota = (await ctx.Servicio.EmitirAsync(ctx.Emisor.Id, null, NotaCreditoA(ctx, factura.Id), "idem-nota")).Comprobante;

        var ex = await Assert.ThrowsAsync<TipoNotaIncompatibleException>(
            () => ctx.Servicio.EmitirAsync(ctx.Emisor.Id, null, NotaCreditoA(ctx, nota.Id), "idem-nota-nota"));

        Assert.Equal(NotaComprobanteValidator.MensajeNotaSobreNota, ex.Message);
    }

    [Fact]
    public async Task AjusteConcurrente_ReintentaConElSaldoNuevoYRechazaSiYaNoAlcanza()
    {
        var ctx = new ComprobanteContexto();
        var factura = await EmitirFacturaAsync(ctx, ctx.FacturaA());
        ctx.Comprobantes.ConflictosSimulados = 2;

        await ctx.Servicio.EmitirAsync(ctx.Emisor.Id, null, NotaCreditoA(ctx, factura.Id), "idem-reintento");

        Assert.Equal(2, ctx.Comprobantes.AjustesRechazados);
        Assert.Equal(10890m, Saldo(ctx, factura.Id));
    }

    [Fact]
    public async Task AjusteConConflictoPersistente_LanzaValidacionSinCrearLaNota()
    {
        var ctx = new ComprobanteContexto();
        var factura = await EmitirFacturaAsync(ctx, ctx.FacturaA());
        ctx.Comprobantes.ConflictosSimulados = 10;

        await Assert.ThrowsAsync<ComprobanteValidationException>(
            () => ctx.Servicio.EmitirAsync(ctx.Emisor.Id, null, NotaCreditoA(ctx, factura.Id), "idem-conflicto"));

        Assert.DoesNotContain(ctx.Comprobantes.Comprobantes, c => c.ComprobanteOriginalId == factura.Id);
    }

    [Fact]
    public async Task FacturaAnteriorSinSaldoGuardado_UsaElImporteTotalComoSaldo()
    {
        var ctx = new ComprobanteContexto();
        var factura = await EmitirFacturaAsync(ctx, ctx.FacturaA());
        ctx.Comprobantes.Comprobantes.Single(c => c.Id == factura.Id).ImporteOriginalAjustado = null;

        await ctx.Servicio.EmitirAsync(ctx.Emisor.Id, null, NotaCreditoA(ctx, factura.Id), "idem-legado");

        Assert.Equal(10890m, Saldo(ctx, factura.Id));
    }

    [Fact]
    public async Task EmisorConsumidorFinal_NoPuedeEmitirNotasA()
    {
        var ctx = new ComprobanteContexto();
        var factura = await EmitirFacturaAsync(ctx, ctx.FacturaA());
        ctx.Emisor.CondicionIva = CondicionIva.ConsumidorFinal;

        var ex = await Assert.ThrowsAsync<EmisionInvalidaException>(
            () => ctx.Servicio.EmitirAsync(ctx.Emisor.Id, null, NotaCreditoA(ctx, factura.Id), "idem-cf"));

        Assert.Equal("Un emisor consumidor final no puede emitir notas A.", ex.Message);
    }

    private static async Task<ComprobanteResponse> EmitirFacturaAsync(ComprobanteContexto ctx, EmitirComprobanteRequest request)
    {
        ctx.Secuencias.Numero++;
        return (await ctx.Servicio.EmitirAsync(ctx.Emisor.Id, null, request, $"idem-{Guid.NewGuid():N}")).Comprobante;
    }

    private static EmitirComprobanteRequest NotaCreditoA(ComprobanteContexto ctx, Guid originalId)
        => NotaSobre(ctx.FacturaA(), TipoComprobante.NotaCreditoA, originalId, 1000m, 1210m, 21m);

    private static EmitirComprobanteRequest NotaSobre(
        EmitirComprobanteRequest factura,
        TipoComprobante tipo,
        Guid originalId,
        decimal precio,
        decimal? total = null,
        decimal? alicuota = null)
        => factura with
        {
            TipoComprobante = (int)tipo,
            ComprobanteOriginalId = originalId,
            Items = [new ItemComprobanteRequest("Ajuste", 1m, precio, alicuota ?? factura.Items![0].AlicuotaIva)],
            ImporteTotal = total ?? precio
        };

    private static decimal? Saldo(ComprobanteContexto ctx, Guid id)
        => ctx.Comprobantes.Comprobantes.Single(c => c.Id == id).ImporteOriginalAjustado;
}
