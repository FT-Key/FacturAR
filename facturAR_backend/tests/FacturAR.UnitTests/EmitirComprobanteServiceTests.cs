using FacturAR.Contracts.Comprobantes;
using FacturAR.Domain.Entities;
using FacturAR.Domain.Enums;
using FacturAR.Domain.Exceptions;

namespace FacturAR.UnitTests;

public sealed class EmitirComprobanteServiceTests
{
    private static readonly DateTime FechaEmision = new(2026, 10, 28, 15, 30, 0, DateTimeKind.Utc);

    [Fact]
    public async Task CP0501_FacturaA_DevuelveImportesNumeroCaeYVencimiento()
    {
        var contexto = new ComprobanteContexto();
        contexto.Secuencias.Numero = 42;

        var respuesta = await contexto.Servicio.EmitirAsync(
            contexto.Emisor.Id,
            "Emisor",
            contexto.FacturaA() with { FechaEmision = FechaEmision },
            "idem-cp0501");

        Assert.NotEqual(Guid.Empty, respuesta.Id);
        Assert.Equal((int)TipoComprobante.FacturaA, respuesta.TipoComprobante);
        Assert.Equal("Factura A", respuesta.TipoComprobanteDescripcion);
        Assert.Equal(1, respuesta.PuntoVenta);
        Assert.Equal(42, respuesta.Numero);
        Assert.Equal(FechaEmision, respuesta.FechaEmision);
        Assert.Matches("^[1-9][0-9]{13}$", respuesta.Cae);
        Assert.Equal(FechaEmision.Date.AddDays(10), respuesta.CaeVencimiento);
        Assert.Equal(10000m, respuesta.ImporteNeto);
        Assert.Equal(2100m, respuesta.ImporteIva);
        Assert.Equal(12100m, respuesta.ImporteTotal);
        Assert.Equal("ARS", respuesta.Moneda);
        Assert.Equal("Emitido", respuesta.Estado);
        Assert.Equal("CUIT", respuesta.Receptor.DocumentoTipo);
        Assert.Equal(ComprobanteContexto.CuitReceptor, respuesta.Receptor.DocumentoNumero);
        Assert.Equal("Cliente S.A.", respuesta.Receptor.RazonSocial);
        Assert.Equal((int)CondicionIva.ResponsableInscripto, respuesta.Receptor.CondicionIva);
        Assert.Equal((contexto.Emisor.Id, 1, TipoComprobante.FacturaA), contexto.Secuencias.UltimaClave);
        Assert.Equal(1, contexto.Transaccion.Ejecuciones);
    }

    [Fact]
    public async Task CP0502_FacturaC_DevuelveImportesSinIva()
    {
        var contexto = new ComprobanteContexto();

        var respuesta = await contexto.Servicio.EmitirAsync(
            contexto.Emisor.Id,
            "Emisor",
            contexto.FacturaC(),
            "idem-cp0502");

        Assert.Equal((int)TipoComprobante.FacturaC, respuesta.TipoComprobante);
        Assert.Equal("Factura C", respuesta.TipoComprobanteDescripcion);
        Assert.Equal(10000m, respuesta.ImporteNeto);
        Assert.Equal(0m, respuesta.ImporteIva);
        Assert.Equal(10000m, respuesta.ImporteTotal);
        Assert.Equal("Emitido", respuesta.Estado);
        Assert.Matches("^[1-9][0-9]{13}$", respuesta.Cae);
    }

    [Fact]
    public async Task CP0503_FacturaB_NoDiscriminaElIvaIncluidoEnElPrecio()
    {
        var contexto = new ComprobanteContexto();

        var respuesta = await contexto.Servicio.EmitirAsync(
            contexto.Emisor.Id,
            "Emisor",
            contexto.FacturaB(),
            "idem-cp0503");

        Assert.Equal((int)TipoComprobante.FacturaB, respuesta.TipoComprobante);
        Assert.Equal("Factura B", respuesta.TipoComprobanteDescripcion);
        Assert.Equal(12100m, respuesta.ImporteNeto);
        Assert.Equal(0m, respuesta.ImporteIva);
        Assert.Equal(12100m, respuesta.ImporteTotal);
    }

    [Fact]
    public async Task CP0517_FacturaCConAlicuotaVeintiuno_NoAplicaElIva()
    {
        var contexto = new ComprobanteContexto();

        var respuesta = await contexto.Servicio.EmitirAsync(
            contexto.Emisor.Id,
            "Emisor",
            contexto.FacturaC(alicuotaIva: 21m),
            "idem-cp0517");

        Assert.Equal(10000m, respuesta.ImporteNeto);
        Assert.Equal(0m, respuesta.ImporteIva);
        Assert.Equal(10000m, respuesta.ImporteTotal);
    }

    [Fact]
    public async Task CP0520_FacturaAConReceptorResponsableInscripto_DiscriminaElIva()
    {
        var contexto = new ComprobanteContexto();

        var respuesta = await contexto.Servicio.EmitirAsync(
            contexto.Emisor.Id,
            "Emisor",
            contexto.FacturaA(),
            "idem-cp0520");

        Assert.Equal(10000m, respuesta.ImporteNeto);
        Assert.Equal(2100m, respuesta.ImporteIva);
        Assert.Equal(12100m, respuesta.ImporteTotal);
        Assert.Equal((int)CondicionIva.ResponsableInscripto, respuesta.Receptor.CondicionIva);
    }

    [Fact]
    public async Task Emitir_ConDiasDeVencimientoConfigurados_UsaElParametro()
    {
        var contexto = new ComprobanteContexto();
        contexto.Parametros.DiasVencimiento = 30;

        var respuesta = await contexto.Servicio.EmitirAsync(
            contexto.Emisor.Id,
            "Emisor",
            contexto.FacturaA() with { FechaEmision = FechaEmision },
            "idem-vencimiento");

        Assert.Equal(FechaEmision.Date.AddDays(30), respuesta.CaeVencimiento);
    }

    [Fact]
    public async Task Emitir_ConMonedaConfigurada_UsaElParametro()
    {
        var contexto = new ComprobanteContexto();
        contexto.Parametros.Moneda = "USD";

        var respuesta = await contexto.Servicio.EmitirAsync(
            contexto.Emisor.Id,
            "Emisor",
            contexto.FacturaC(),
            "idem-moneda");

        Assert.Equal("USD", respuesta.Moneda);
    }

    [Fact]
    public async Task CP0506_ImporteTotalInconsistente_LanzaValidacionSinConsumirNumero()
    {
        var contexto = new ComprobanteContexto();

        var ex = await Assert.ThrowsAsync<ComprobanteValidationException>(
            () => contexto.Servicio.EmitirAsync(
                contexto.Emisor.Id,
                "Emisor",
                contexto.FacturaA(importeTotal: 9999m),
                "idem-cp0506"));

        Assert.Equal("El importe total no coincide con el cálculo de los ítems.", ex.Message);
        Assert.Equal(0, contexto.Secuencias.Llamadas);
        Assert.Equal(0, contexto.Transaccion.Ejecuciones);
        Assert.Empty(contexto.Comprobantes.Comprobantes);
        Assert.Empty(contexto.Eventos.Eventos);
    }

    [Fact]
    public async Task CP0507_PuntoDeVentaDeOtroContribuyente_LanzaNoEncontradoSinConsumirNumero()
    {
        var contexto = new ComprobanteContexto();

        var ex = await Assert.ThrowsAsync<ComprobanteNoEncontradoException>(
            () => contexto.Servicio.EmitirAsync(
                contexto.Emisor.Id,
                "Emisor",
                contexto.FacturaA() with { PuntoVenta = 2 },
                "idem-cp0507"));

        Assert.Equal(
            "El punto de venta no existe o no pertenece al contribuyente autenticado.",
            ex.Message);
        Assert.Equal(0, contexto.Secuencias.Llamadas);
        Assert.Empty(contexto.Comprobantes.Comprobantes);
    }

    [Fact]
    public async Task CP0508_PuntoDeVentaInactivo_LanzaPuntoVentaInactivoSinConsumirNumero()
    {
        var contexto = new ComprobanteContexto();
        contexto.PuntosVenta.Agregar(new PuntoVenta
        {
            ContribuyenteId = contexto.Emisor.Id,
            Numero = 3,
            Descripcion = "Inactivo",
            Activo = false
        });

        var ex = await Assert.ThrowsAsync<PuntoVentaInactivoException>(
            () => contexto.Servicio.EmitirAsync(
                contexto.Emisor.Id,
                "Emisor",
                contexto.FacturaA() with { PuntoVenta = 3 },
                "idem-cp0508"));

        Assert.Equal("El punto de venta está inactivo.", ex.Message);
        Assert.Equal(0, contexto.Secuencias.Llamadas);
        Assert.Empty(contexto.Comprobantes.Comprobantes);
    }

    [Fact]
    public async Task CP0510_RolConsulta_LanzaSinPermisosSinTocarLosRepositorios()
    {
        var contexto = new ComprobanteContexto();

        var ex = await Assert.ThrowsAsync<SinPermisosException>(
            () => contexto.Servicio.EmitirAsync(
                contexto.Emisor.Id,
                "Consulta",
                contexto.FacturaA(),
                "idem-cp0510"));

        Assert.Equal("El rol del principal no permite emitir comprobantes.", ex.Message);
        Assert.Equal(0, contexto.Contribuyentes.Consultas);
        Assert.Equal(0, contexto.PuntosVenta.Consultas);
        Assert.Equal(0, contexto.Secuencias.Llamadas);
        Assert.Equal(0, contexto.Transaccion.Ejecuciones);
        Assert.Empty(contexto.Comprobantes.Comprobantes);
    }

    [Fact]
    public async Task Emitir_ConRolNuloPorApiKey_EmiteElComprobante()
    {
        var contexto = new ComprobanteContexto();

        var respuesta = await contexto.Servicio.EmitirAsync(
            contexto.Emisor.Id,
            null,
            contexto.FacturaC(),
            "idem-apikey");

        Assert.Equal("Emitido", respuesta.Estado);
        Assert.Single(contexto.Comprobantes.Comprobantes);
    }

    [Theory]
    [InlineData("Administrador")]
    [InlineData("Emisor")]
    public async Task Emitir_ConRolesHabilitados_EmiteElComprobante(string rol)
    {
        var contexto = new ComprobanteContexto();

        var respuesta = await contexto.Servicio.EmitirAsync(
            contexto.Emisor.Id,
            rol,
            contexto.FacturaC(),
            $"idem-{rol}");

        Assert.Equal("Emitido", respuesta.Estado);
    }

    [Fact]
    public async Task CP0511_ContribuyenteInactivo_LanzaCuentaInactivaSinConsumirNumero()
    {
        var contexto = new ComprobanteContexto();
        contexto.Emisor.Activo = false;

        await Assert.ThrowsAsync<CuentaInactivaException>(
            () => contexto.Servicio.EmitirAsync(
                contexto.Emisor.Id,
                "Emisor",
                contexto.FacturaA(),
                "idem-cp0511"));

        Assert.Equal(0, contexto.Secuencias.Llamadas);
        Assert.Empty(contexto.Comprobantes.Comprobantes);
    }

    [Fact]
    public async Task CP0511_ContribuyenteInexistente_LanzaCuentaInactivaSinConsumirNumero()
    {
        var contexto = new ComprobanteContexto();

        await Assert.ThrowsAsync<CuentaInactivaException>(
            () => contexto.Servicio.EmitirAsync(
                Guid.NewGuid(),
                "Emisor",
                contexto.FacturaA(),
                "idem-inexistente"));

        Assert.Equal(0, contexto.Secuencias.Llamadas);
        Assert.Empty(contexto.Comprobantes.Comprobantes);
    }

    [Fact]
    public async Task CP0519_EmisorConsumidorFinalConFacturaA_LanzaEmisionInvalida()
    {
        var contexto = new ComprobanteContexto();
        contexto.Emisor.CondicionIva = CondicionIva.ConsumidorFinal;

        var ex = await Assert.ThrowsAsync<EmisionInvalidaException>(
            () => contexto.Servicio.EmitirAsync(
                contexto.Emisor.Id,
                "Emisor",
                contexto.FacturaA(),
                "idem-cp0519"));

        Assert.Equal("Un emisor consumidor final no puede emitir una factura A.", ex.Message);
        Assert.Equal(0, contexto.Secuencias.Llamadas);
        Assert.Empty(contexto.Comprobantes.Comprobantes);
    }

    [Fact]
    public async Task Emitir_PersisteElComprobanteConLosDatosDelContextoYElEventoDeAuditoria()
    {
        var contexto = new ComprobanteContexto();

        var respuesta = await contexto.Servicio.EmitirAsync(
            contexto.Emisor.Id,
            "Administrador",
            contexto.FacturaA(),
            "idem-persistencia");

        var guardado = Assert.Single(contexto.Comprobantes.Comprobantes);
        Assert.Equal(respuesta.Id, guardado.Id);
        Assert.Equal("idem-persistencia", guardado.IdempotencyKey);
        Assert.Equal(contexto.Emisor.Id, guardado.ContribuyenteId);
        Assert.Equal(contexto.Emisor.Cuit, guardado.EmisorCuit);
        Assert.Equal(contexto.Emisor.RazonSocial, guardado.EmisorRazonSocial);
        Assert.Equal(1, guardado.PuntoVenta);
        Assert.Equal(TipoComprobante.FacturaA, guardado.TipoComprobante);
        Assert.Equal(respuesta.Numero, guardado.Numero);
        Assert.Equal(respuesta.Cae, guardado.Cae);
        Assert.Equal(EstadoComprobante.Emitido, guardado.Estado);
        Assert.Equal(10000m, guardado.ImporteNeto);
        Assert.Equal(2100m, guardado.ImporteIva);
        Assert.Equal(12100m, guardado.ImporteTotal);
        Assert.Equal("ARS", guardado.Moneda);
        Assert.Equal(DateTimeKind.Utc, guardado.CreatedAt.Kind);
        Assert.Equal(DateTimeKind.Utc, guardado.FechaEmision.Kind);
        Assert.Equal(DateTimeKind.Utc, guardado.CaeVencimiento.Kind);

        var item = Assert.Single(guardado.Items);
        Assert.Equal("Servicio", item.Descripcion);
        Assert.Equal(2m, item.Cantidad);
        Assert.Equal(5000m, item.PrecioUnitario);
        Assert.Equal(21m, item.AlicuotaIva);
        Assert.Equal(10000m, item.Subtotal);

        var evento = Assert.Single(contexto.Eventos.Eventos);
        Assert.Equal("comprobante.emitido", evento.Accion);
        Assert.Equal("Comprobante", evento.Entidad);
        Assert.Equal(guardado.Id.ToString(), evento.EntidadId);
        Assert.Equal(contexto.Emisor.Id, evento.ContribuyenteId);
        Assert.Null(evento.UsuarioId);
        Assert.Equal("Empresa", evento.ActorTipo);
        Assert.Contains(respuesta.Cae, evento.Datos ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains("\"numero\":\"42\"", evento.Datos ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Emitir_ConItemsNulos_LanzaValidacionSinExcepcionDeReferenciaNula()
    {
        var contexto = new ComprobanteContexto();
        var request = contexto.FacturaA() with
        {
            Items = new List<ItemComprobanteRequest> { null! }
        };

        var ex = await Assert.ThrowsAsync<ComprobanteValidationException>(
            () => contexto.Servicio.EmitirAsync(contexto.Emisor.Id, "Emisor", request, "idem-items-nulos"));

        Assert.Equal("Los ítems del comprobante no son válidos.", ex.Message);
        Assert.Equal(0, contexto.Secuencias.Llamadas);
        Assert.Empty(contexto.Comprobantes.Comprobantes);
    }

    [Fact]
    public async Task Emitir_SinItems_LanzaValidacionSinConsumirNumero()
    {
        var contexto = new ComprobanteContexto();
        var request = contexto.FacturaA() with { Items = null };

        var ex = await Assert.ThrowsAsync<ComprobanteValidationException>(
            () => contexto.Servicio.EmitirAsync(contexto.Emisor.Id, "Emisor", request, "idem-sin-items"));

        Assert.Equal("El comprobante debe tener entre 1 y 500 ítems.", ex.Message);
        Assert.Equal(0, contexto.Secuencias.Llamadas);
    }

    [Fact]
    public async Task Emitir_ConFechaExtrema_LanzaValidacionSinConsumirNumero()
    {
        var contexto = new ComprobanteContexto();
        var request = contexto.FacturaA() with
        {
            FechaEmision = new DateTime(9999, 12, 31, 0, 0, 0, DateTimeKind.Utc)
        };

        var ex = await Assert.ThrowsAsync<ComprobanteValidationException>(
            () => contexto.Servicio.EmitirAsync(contexto.Emisor.Id, "Emisor", request, "idem-fecha-extrema"));

        Assert.Equal("La fecha de emisión no es válida.", ex.Message);
        Assert.Equal(0, contexto.Secuencias.Llamadas);
        Assert.Empty(contexto.Comprobantes.Comprobantes);
    }

    [Fact]
    public async Task Emitir_ConAlicuotaNoPermitida_LanzaValidacionSinConsumirNumero()
    {
        var contexto = new ComprobanteContexto();

        var ex = await Assert.ThrowsAsync<ComprobanteValidationException>(
            () => contexto.Servicio.EmitirAsync(
                contexto.Emisor.Id,
                "Emisor",
                contexto.FacturaA(alicuotaIva: 12m),
                "idem-cp0516"));

        Assert.Equal("La alícuota de IVA no está permitida.", ex.Message);
        Assert.Equal(0, contexto.Secuencias.Llamadas);
    }

    [Fact]
    public void RequestDeEmision_NoExponeElContribuyenteNiElEmisor()
    {
        var propiedades = typeof(EmitirComprobanteRequest)
            .GetProperties()
            .Select(p => p.Name)
            .ToList();

        Assert.DoesNotContain("ContribuyenteId", propiedades);
        Assert.DoesNotContain("Contribuyente", propiedades);
        Assert.DoesNotContain("EmisorCuit", propiedades);
        Assert.DoesNotContain("Cae", propiedades);
        Assert.DoesNotContain("Numero", propiedades);
    }
}
