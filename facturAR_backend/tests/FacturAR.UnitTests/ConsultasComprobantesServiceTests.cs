using FacturAR.Application.Abstractions;
using FacturAR.Application.Comprobantes;
using FacturAR.Domain.Entities;
using FacturAR.Domain.Enums;
using FacturAR.Domain.Exceptions;

namespace FacturAR.UnitTests;

public sealed class ConsultasComprobantesServiceTests
{
    private static readonly Guid ContribuyenteId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtroContribuyenteId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTime FechaEmision = new(2026, 11, 30, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ListarAsync_SinParametros_UsaPageUnoYPageSizeVeinteYPropagaElContribuyente()
    {
        var repositorio = new ConsultaComprobantesRepositorioFake();
        var servicio = new ListarComprobantesService(repositorio);

        var pagina = await servicio.ListarAsync(
            ContribuyenteId,
            page: null,
            pageSize: null,
            desde: null,
            hasta: null,
            tipo: null,
            estado: null,
            puntoVenta: null,
            receptor: null);

        Assert.Equal(1, pagina.Page);
        Assert.Equal(20, pagina.PageSize);
        Assert.Equal(0, pagina.Total);
        Assert.Equal(0, pagina.TotalPages);
        Assert.Empty(pagina.Items);
        Assert.Equal(ContribuyenteId, repositorio.UltimoContribuyenteId);
        Assert.Equal(1, repositorio.UltimaPage);
        Assert.Equal(20, repositorio.UltimoPageSize);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public async Task ListarAsync_ConPageMenorAUno_LanzaValidacion(int page)
    {
        var servicio = new ListarComprobantesService(new ConsultaComprobantesRepositorioFake());

        await Assert.ThrowsAsync<ComprobanteValidationException>(() => servicio.ListarAsync(
            ContribuyenteId,
            page,
            20,
            null,
            null,
            null,
            null,
            null,
            null));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(500)]
    public async Task ListarAsync_ConPageSizeFueraDeRango_LanzaValidacion(int pageSize)
    {
        var servicio = new ListarComprobantesService(new ConsultaComprobantesRepositorioFake());

        await Assert.ThrowsAsync<ComprobanteValidationException>(() => servicio.ListarAsync(
            ContribuyenteId,
            1,
            pageSize,
            null,
            null,
            null,
            null,
            null,
            null));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public async Task ListarAsync_ConPageSizeEnElLimite_AceptaElValor(int pageSize)
    {
        var repositorio = new ConsultaComprobantesRepositorioFake();
        var servicio = new ListarComprobantesService(repositorio);

        var pagina = await servicio.ListarAsync(
            ContribuyenteId,
            1,
            pageSize,
            null,
            null,
            null,
            null,
            null,
            null);

        Assert.Equal(pageSize, pagina.PageSize);
        Assert.Equal(pageSize, repositorio.UltimoPageSize);
    }

    [Fact]
    public async Task ListarAsync_ConDesdeYHastaSoloFecha_IncluyeElDiaCompletoEnUtc()
    {
        var repositorio = new ConsultaComprobantesRepositorioFake();
        var servicio = new ListarComprobantesService(repositorio);

        await servicio.ListarAsync(
            ContribuyenteId,
            1,
            20,
            "2026-11-01",
            "2026-11-30",
            null,
            null,
            null,
            null);

        var filtro = Assert.IsType<FiltroListadoComprobantes>(repositorio.UltimoFiltro);
        Assert.Equal(Utc(2026, 11, 1), filtro.Desde);
        Assert.Equal(DateTimeKind.Utc, filtro.Desde!.Value.Kind);
        Assert.Equal(Utc(2026, 12, 1), filtro.HastaExclusivo);
        Assert.Equal(DateTimeKind.Utc, filtro.HastaExclusivo!.Value.Kind);
    }

    [Fact]
    public async Task ListarAsync_ConDesdeIsoConZ_MantieneElInstanteUtc()
    {
        var repositorio = new ConsultaComprobantesRepositorioFake();
        var servicio = new ListarComprobantesService(repositorio);

        await servicio.ListarAsync(
            ContribuyenteId,
            1,
            20,
            "2026-11-01T12:30:00Z",
            null,
            null,
            null,
            null,
            null);

        var filtro = Assert.IsType<FiltroListadoComprobantes>(repositorio.UltimoFiltro);
        Assert.Equal(new DateTime(2026, 11, 1, 12, 30, 0, DateTimeKind.Utc), filtro.Desde);
        Assert.Null(filtro.HastaExclusivo);
    }

    [Fact]
    public async Task ListarAsync_ConDesdeIsoConOffset_ConvierteAUtc()
    {
        var repositorio = new ConsultaComprobantesRepositorioFake();
        var servicio = new ListarComprobantesService(repositorio);

        await servicio.ListarAsync(
            ContribuyenteId,
            1,
            20,
            "2026-11-01T09:30:00-03:00",
            null,
            null,
            null,
            null,
            null);

        var filtro = Assert.IsType<FiltroListadoComprobantes>(repositorio.UltimoFiltro);
        Assert.Equal(new DateTime(2026, 11, 1, 12, 30, 0, DateTimeKind.Utc), filtro.Desde);
    }

    [Fact]
    public async Task ListarAsync_ConHastaIsoConHora_IncluyeTodoElDiaSiguienteComoLimiteExclusivo()
    {
        var repositorio = new ConsultaComprobantesRepositorioFake();
        var servicio = new ListarComprobantesService(repositorio);

        await servicio.ListarAsync(
            ContribuyenteId,
            1,
            20,
            null,
            "2026-11-30T15:00:00Z",
            null,
            null,
            null,
            null);

        var filtro = Assert.IsType<FiltroListadoComprobantes>(repositorio.UltimoFiltro);
        Assert.Null(filtro.Desde);
        Assert.Equal(Utc(2026, 12, 1), filtro.HastaExclusivo);
    }

    [Fact]
    public async Task ListarAsync_ConDesdePosteriorAHasta_LanzaValidacion()
    {
        var servicio = new ListarComprobantesService(new ConsultaComprobantesRepositorioFake());

        await Assert.ThrowsAsync<ComprobanteValidationException>(() => servicio.ListarAsync(
            ContribuyenteId,
            1,
            20,
            "2026-12-01",
            "2026-11-30",
            null,
            null,
            null,
            null));
    }

    [Theory]
    [InlineData("no-es-una-fecha", null)]
    [InlineData(null, "2026-13-45")]
    [InlineData("2026-11-01T99:00:00Z", null)]
    public async Task ListarAsync_ConFechaInvalida_LanzaValidacion(string? desde, string? hasta)
    {
        var servicio = new ListarComprobantesService(new ConsultaComprobantesRepositorioFake());

        await Assert.ThrowsAsync<ComprobanteValidationException>(() => servicio.ListarAsync(
            ContribuyenteId,
            1,
            20,
            desde,
            hasta,
            null,
            null,
            null,
            null));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(99)]
    public async Task ListarAsync_ConTipoInvalido_LanzaValidacion(int tipo)
    {
        var servicio = new ListarComprobantesService(new ConsultaComprobantesRepositorioFake());

        await Assert.ThrowsAsync<ComprobanteValidationException>(() => servicio.ListarAsync(
            ContribuyenteId,
            1,
            20,
            null,
            null,
            tipo,
            null,
            null,
            null));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    [InlineData(9)]
    public async Task ListarAsync_ConEstadoInvalido_LanzaValidacion(int estado)
    {
        var servicio = new ListarComprobantesService(new ConsultaComprobantesRepositorioFake());

        await Assert.ThrowsAsync<ComprobanteValidationException>(() => servicio.ListarAsync(
            ContribuyenteId,
            1,
            20,
            null,
            null,
            null,
            estado,
            null,
            null));
    }

    [Fact]
    public async Task ListarAsync_ConTipoYEstadoValidos_LosPasaAlFiltro()
    {
        var repositorio = new ConsultaComprobantesRepositorioFake();
        var servicio = new ListarComprobantesService(repositorio);

        await servicio.ListarAsync(
            ContribuyenteId,
            1,
            20,
            null,
            null,
            (int)TipoComprobante.NotaCreditoC,
            (int)EstadoComprobante.Anulado,
            null,
            null);

        var filtro = Assert.IsType<FiltroListadoComprobantes>(repositorio.UltimoFiltro);
        Assert.Equal(TipoComprobante.NotaCreditoC, filtro.Tipo);
        Assert.Equal(EstadoComprobante.Anulado, filtro.Estado);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ListarAsync_ConPuntoVentaMenorAUno_LanzaValidacion(int puntoVenta)
    {
        var servicio = new ListarComprobantesService(new ConsultaComprobantesRepositorioFake());

        await Assert.ThrowsAsync<ComprobanteValidationException>(() => servicio.ListarAsync(
            ContribuyenteId,
            1,
            20,
            null,
            null,
            null,
            null,
            puntoVenta,
            null));
    }

    [Fact]
    public async Task ListarAsync_ConPuntoVentaValido_LoPasaAlFiltro()
    {
        var repositorio = new ConsultaComprobantesRepositorioFake();
        var servicio = new ListarComprobantesService(repositorio);

        await servicio.ListarAsync(
            ContribuyenteId,
            1,
            20,
            null,
            null,
            null,
            null,
            2,
            null);

        var filtro = Assert.IsType<FiltroListadoComprobantes>(repositorio.UltimoFiltro);
        Assert.Equal(2, filtro.PuntoVenta);
    }

    [Theory]
    [InlineData("20-12345678-6", "20123456786")]
    [InlineData(" 20 1234 5678 6 ", "20123456786")]
    [InlineData("20123456786", "20123456786")]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("---", null)]
    public async Task ListarAsync_ConReceptor_QuitaGuionesYEspacios(string? receptor, string? esperado)
    {
        var repositorio = new ConsultaComprobantesRepositorioFake();
        var servicio = new ListarComprobantesService(repositorio);

        await servicio.ListarAsync(
            ContribuyenteId,
            1,
            20,
            null,
            null,
            null,
            null,
            null,
            receptor);

        var filtro = Assert.IsType<FiltroListadoComprobantes>(repositorio.UltimoFiltro);
        Assert.Equal(esperado, filtro.ReceptorDocumentoNumero);
    }

    [Fact]
    public async Task ListarAsync_ConTotalCero_DevuelveTotalPagesCero()
    {
        var repositorio = new ConsultaComprobantesRepositorioFake();
        var servicio = new ListarComprobantesService(repositorio);

        var pagina = await servicio.ListarAsync(
            ContribuyenteId,
            1,
            10,
            null,
            null,
            null,
            null,
            null,
            null);

        Assert.Equal(0, pagina.Total);
        Assert.Equal(0, pagina.TotalPages);
    }

    [Fact]
    public async Task ListarAsync_ConTotalVeinticincoYPageSizeDiez_CalculaTresPaginas()
    {
        var repositorio = new ConsultaComprobantesRepositorioFake { TotalPersonalizado = 25 };
        var servicio = new ListarComprobantesService(repositorio);

        var pagina = await servicio.ListarAsync(
            ContribuyenteId,
            1,
            10,
            null,
            null,
            null,
            null,
            null,
            null);

        Assert.Equal(25, pagina.Total);
        Assert.Equal(3, pagina.TotalPages);
    }

    [Fact]
    public async Task ListarAsync_MapeaElResumenConLosCamposDelContrato()
    {
        var comprobante = NuevoComprobante();
        comprobante.Estado = EstadoComprobante.Anulado;
        var repositorio = new ConsultaComprobantesRepositorioFake();
        repositorio.Comprobantes.Add(comprobante);
        var servicio = new ListarComprobantesService(repositorio);

        var pagina = await servicio.ListarAsync(
            ContribuyenteId,
            1,
            20,
            null,
            null,
            null,
            null,
            null,
            null);

        var item = Assert.Single(pagina.Items);
        Assert.Equal(comprobante.Id, item.Id);
        Assert.Equal((int)TipoComprobante.FacturaC, item.TipoComprobante);
        Assert.Equal("Factura C", item.TipoComprobanteDescripcion);
        Assert.Equal(1, item.PuntoVenta);
        Assert.Equal(42, item.Numero);
        Assert.Equal(FechaEmision, item.FechaEmision);
        Assert.Equal("12345678901234", item.Cae);
        Assert.Equal("Cliente Final", item.ReceptorRazonSocial);
        Assert.Equal("20345678901", item.ReceptorDocumentoNumero);
        Assert.Equal(12100m, item.ImporteTotal);
        Assert.Equal("ARS", item.Moneda);
        Assert.Equal(nameof(EstadoComprobante.Anulado), item.Estado);
    }

    [Fact]
    public async Task ConsultarAsync_ConComprobantePropio_DevuelveElDetalleConItems()
    {
        var comprobante = NuevoComprobante();
        comprobante.Items =
        [
            new ComprobanteItem
            {
                Descripcion = "Servicio",
                Cantidad = 2m,
                PrecioUnitario = 5000m,
                AlicuotaIva = 21m,
                Subtotal = 10000m
            },
            new ComprobanteItem
            {
                Descripcion = "Producto",
                Cantidad = 1m,
                PrecioUnitario = 2100m,
                AlicuotaIva = 21m,
                Subtotal = 2100m
            }
        ];
        var repositorio = new ConsultaComprobantesRepositorioFake();
        repositorio.Comprobantes.Add(comprobante);
        var servicio = new ConsultarComprobanteService(repositorio);

        var detalle = await servicio.ConsultarAsync(ContribuyenteId, comprobante.Id);

        Assert.NotNull(detalle);
        Assert.Equal(comprobante.Id, detalle.Id);
        Assert.Equal((int)TipoComprobante.FacturaC, detalle.TipoComprobante);
        Assert.Equal("Factura C", detalle.TipoComprobanteDescripcion);
        Assert.Equal(1, detalle.PuntoVenta);
        Assert.Equal(42, detalle.Numero);
        Assert.Equal(FechaEmision, detalle.FechaEmision);
        Assert.Equal("12345678901234", detalle.Cae);
        Assert.Equal(Utc(2026, 12, 10), detalle.CaeVencimiento);
        Assert.Equal("20123456786", detalle.EmisorCuit);
        Assert.Equal("Demo SRL", detalle.EmisorRazonSocial);
        Assert.Equal("CUIT", detalle.Receptor.DocumentoTipo);
        Assert.Equal("20345678901", detalle.Receptor.DocumentoNumero);
        Assert.Equal("Cliente Final", detalle.Receptor.RazonSocial);
        Assert.Equal((int)CondicionIva.ResponsableInscripto, detalle.Receptor.CondicionIva);
        Assert.Equal(10000m, detalle.ImporteNeto);
        Assert.Equal(2100m, detalle.ImporteIva);
        Assert.Equal(12100m, detalle.ImporteTotal);
        Assert.Equal("ARS", detalle.Moneda);
        Assert.Equal(nameof(EstadoComprobante.Emitido), detalle.Estado);
        Assert.Equal("Observación de prueba", detalle.Observaciones);
        Assert.Equal(2, detalle.Items.Count);
        Assert.Equal("Servicio", detalle.Items[0].Descripcion);
        Assert.Equal(2m, detalle.Items[0].Cantidad);
        Assert.Equal(5000m, detalle.Items[0].PrecioUnitario);
        Assert.Equal(21m, detalle.Items[0].AlicuotaIva);
        Assert.Equal(10000m, detalle.Items[0].Subtotal);
        Assert.Equal("Producto", detalle.Items[1].Descripcion);
        Assert.Equal(ContribuyenteId, repositorio.UltimoContribuyenteId);
        Assert.Equal(comprobante.Id, repositorio.UltimoId);
    }

    [Fact]
    public async Task ConsultarAsync_ConComprobanteDeOtraEmpresa_DevuelveNull()
    {
        var comprobante = NuevoComprobante();
        comprobante.ContribuyenteId = OtroContribuyenteId;
        var repositorio = new ConsultaComprobantesRepositorioFake();
        repositorio.Comprobantes.Add(comprobante);
        var servicio = new ConsultarComprobanteService(repositorio);

        var detalle = await servicio.ConsultarAsync(ContribuyenteId, comprobante.Id);

        Assert.Null(detalle);
        Assert.Equal(ContribuyenteId, repositorio.UltimoContribuyenteId);
    }

    [Fact]
    public async Task ConsultarPorNumeroAsync_ConComprobantePropio_DevuelveElDetalle()
    {
        var comprobante = NuevoComprobante();
        var repositorio = new ConsultaComprobantesRepositorioFake();
        repositorio.Comprobantes.Add(comprobante);
        var servicio = new ConsultarComprobanteService(repositorio);

        var detalle = await servicio.ConsultarPorNumeroAsync(
            ContribuyenteId,
            (int)TipoComprobante.FacturaC,
            1,
            42);

        Assert.NotNull(detalle);
        Assert.Equal(comprobante.Id, detalle.Id);
        Assert.Equal(42, detalle.Numero);
        Assert.Equal("12345678901234", detalle.Cae);
        Assert.Equal(ContribuyenteId, repositorio.UltimoContribuyenteId);
    }

    [Fact]
    public async Task ConsultarPorNumeroAsync_ConComprobanteDeOtraEmpresa_DevuelveNull()
    {
        var comprobante = NuevoComprobante();
        comprobante.ContribuyenteId = OtroContribuyenteId;
        var repositorio = new ConsultaComprobantesRepositorioFake();
        repositorio.Comprobantes.Add(comprobante);
        var servicio = new ConsultarComprobanteService(repositorio);

        var detalle = await servicio.ConsultarPorNumeroAsync(
            ContribuyenteId,
            (int)TipoComprobante.FacturaC,
            1,
            42);

        Assert.Null(detalle);
        Assert.Equal(ContribuyenteId, repositorio.UltimoContribuyenteId);
    }

    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(99, 1, 1)]
    [InlineData(11, 0, 1)]
    [InlineData(11, -1, 1)]
    [InlineData(11, 1, 0)]
    [InlineData(11, 1, -5)]
    public async Task ConsultarPorNumeroAsync_ConParametrosInvalidos_LanzaValidacionSinConsultar(
        int tipo,
        int puntoVenta,
        long numero)
    {
        var repositorio = new ConsultaComprobantesRepositorioFake();
        var servicio = new ConsultarComprobanteService(repositorio);

        await Assert.ThrowsAsync<ComprobanteValidationException>(
            () => servicio.ConsultarPorNumeroAsync(ContribuyenteId, tipo, puntoVenta, numero));

        Assert.Equal(0, repositorio.ConsultasDetalle);
    }

    [Fact]
    public async Task ConsultarUltimoEmitidoAsync_IgnoraAnuladosYDevuelveElMayorEmitidoDelContribuyente()
    {
        var repositorio = new ConsultaComprobantesRepositorioFake();
        var emitido = NuevoComprobante();
        var anuladoMayor = NuevoComprobante();
        anuladoMayor.Numero = 43;
        anuladoMayor.Estado = EstadoComprobante.Anulado;
        var ajenoMayor = NuevoComprobante();
        ajenoMayor.ContribuyenteId = OtroContribuyenteId;
        ajenoMayor.Numero = 99;
        repositorio.Comprobantes.AddRange([emitido, anuladoMayor, ajenoMayor]);
        var servicio = new ConsultarComprobanteService(repositorio);

        var ultimo = await servicio.ConsultarUltimoEmitidoAsync(
            ContribuyenteId,
            (int)TipoComprobante.FacturaC,
            1);

        Assert.NotNull(ultimo);
        Assert.Equal(42, ultimo.Numero);
        Assert.Equal(emitido.Cae, ultimo.Cae);
    }

    [Fact]
    public async Task ObtenerAsync_ConPuntoVenta_DevuelveElNumeroDeLaSecuenciaDelContribuyente()
    {
        var repositorio = new ConsultaSecuenciasRepositorioFake { UltimoNumero = 7 };
        var servicio = new ObtenerUltimoAutorizadoService(repositorio);

        var resultado = await servicio.ObtenerAsync(
            ContribuyenteId,
            (int)TipoComprobante.FacturaC,
            2);

        Assert.Equal((int)TipoComprobante.FacturaC, resultado.TipoComprobante);
        Assert.Equal(2, resultado.PuntoVenta);
        Assert.Equal(7, resultado.Numero);
        Assert.Equal(ContribuyenteId, repositorio.UltimoContribuyenteId);
        Assert.Equal(2, repositorio.UltimoPuntoVenta);
        Assert.Equal(TipoComprobante.FacturaC, repositorio.UltimoTipo);
        Assert.False(repositorio.ConsultoMaximo);
    }

    [Fact]
    public async Task ObtenerAsync_SinPuntoVenta_DevuelveElMaximoAgregadoPorTipo()
    {
        var repositorio = new ConsultaSecuenciasRepositorioFake { MaximoUltimoNumero = 42 };
        var servicio = new ObtenerUltimoAutorizadoService(repositorio);

        var resultado = await servicio.ObtenerAsync(
            ContribuyenteId,
            (int)TipoComprobante.FacturaC,
            null);

        Assert.Equal((int)TipoComprobante.FacturaC, resultado.TipoComprobante);
        Assert.Null(resultado.PuntoVenta);
        Assert.Equal(42, resultado.Numero);
        Assert.Equal(ContribuyenteId, repositorio.UltimoContribuyenteId);
        Assert.Equal(TipoComprobante.FacturaC, repositorio.UltimoTipo);
        Assert.True(repositorio.ConsultoMaximo);
    }

    [Fact]
    public async Task ObtenerAsync_SinSecuencia_DevuelveCero()
    {
        var servicio = new ObtenerUltimoAutorizadoService(new ConsultaSecuenciasRepositorioFake());

        var resultado = await servicio.ObtenerAsync(
            ContribuyenteId,
            (int)TipoComprobante.NotaDebitoA,
            null);

        Assert.Equal(0, resultado.Numero);
        Assert.Null(resultado.PuntoVenta);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(99)]
    public async Task ObtenerAsync_ConTipoFaltanteOInvalido_LanzaValidacion(int? tipo)
    {
        var repositorio = new ConsultaSecuenciasRepositorioFake();
        var servicio = new ObtenerUltimoAutorizadoService(repositorio);

        await Assert.ThrowsAsync<ComprobanteValidationException>(
            () => servicio.ObtenerAsync(ContribuyenteId, tipo, 1));

        Assert.Equal(0, repositorio.Consultas);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ObtenerAsync_ConPuntoVentaMenorAUno_LanzaValidacion(int puntoVenta)
    {
        var repositorio = new ConsultaSecuenciasRepositorioFake();
        var servicio = new ObtenerUltimoAutorizadoService(repositorio);

        await Assert.ThrowsAsync<ComprobanteValidationException>(
            () => servicio.ObtenerAsync(ContribuyenteId, (int)TipoComprobante.FacturaA, puntoVenta));

        Assert.Equal(0, repositorio.Consultas);
    }

    [Fact]
    public async Task ConsultarPorNumeroAsync_ConComprobantePropio_DevuelveElDetalle()
    {
        var comprobante = NuevoComprobante();
        var repositorio = new ConsultaComprobantesRepositorioFake();
        repositorio.Comprobantes.Add(comprobante);
        var servicio = new ConsultarComprobanteService(repositorio);

        var detalle = await servicio.ConsultarPorNumeroAsync(
            ContribuyenteId,
            1,
            (int)TipoComprobante.FacturaC,
            42);

        Assert.NotNull(detalle);
        Assert.Equal(comprobante.Id, detalle.Id);
        Assert.Equal(1, detalle.PuntoVenta);
        Assert.Equal((int)TipoComprobante.FacturaC, detalle.TipoComprobante);
        Assert.Equal(42, detalle.Numero);
        Assert.Equal("12345678901234", detalle.Cae);
    }

    [Fact]
    public async Task ConsultarPorNumeroAsync_DeOtraEmpresa_DevuelveNull()
    {
        var comprobante = NuevoComprobante();
        comprobante.ContribuyenteId = OtroContribuyenteId;
        var repositorio = new ConsultaComprobantesRepositorioFake();
        repositorio.Comprobantes.Add(comprobante);
        var servicio = new ConsultarComprobanteService(repositorio);

        var detalle = await servicio.ConsultarPorNumeroAsync(
            ContribuyenteId,
            1,
            (int)TipoComprobante.FacturaC,
            42);

        Assert.Null(detalle);
    }

    [Theory]
    [InlineData(0, 11, 1)]
    [InlineData(10000, 11, 1)]
    [InlineData(1, 0, 1)]
    [InlineData(1, 99, 1)]
    [InlineData(1, 11, 0)]
    [InlineData(1, 11, -5)]
    public async Task ConsultarPorNumeroAsync_ConParametrosInvalidos_LanzaValidacion(
        int puntoVenta,
        int tipoComprobante,
        long numero)
    {
        var repositorio = new ConsultaComprobantesRepositorioFake();
        var servicio = new ConsultarComprobanteService(repositorio);

        await Assert.ThrowsAsync<ComprobanteValidationException>(
            () => servicio.ConsultarPorNumeroAsync(ContribuyenteId, puntoVenta, tipoComprobante, numero));
    }

    [Fact]
    public async Task ConsultarUltimoEmitidoAsync_ConVariosComprobantes_DevuelveElMayorNumero()
    {
        var primero = NuevoComprobante();
        primero.Numero = 1;
        primero.FechaEmision = Utc(2026, 11, 1);
        var ultimo = NuevoComprobante();
        ultimo.Numero = 9;
        ultimo.FechaEmision = Utc(2026, 11, 5);
        var repositorio = new ConsultaComprobantesRepositorioFake();
        repositorio.Comprobantes.AddRange([primero, ultimo]);
        var servicio = new ConsultarComprobanteService(repositorio);

        var detalle = await servicio.ConsultarUltimoEmitidoAsync(
            ContribuyenteId,
            1,
            (int)TipoComprobante.FacturaC);

        Assert.NotNull(detalle);
        Assert.Equal(9, detalle.Numero);
        Assert.Equal(Utc(2026, 11, 5), detalle.FechaEmision);
    }

    private static DateTime Utc(int anio, int mes, int dia)
        => new(anio, mes, dia, 0, 0, 0, DateTimeKind.Utc);

    private static Comprobante NuevoComprobante()
        => new()
        {
            ContribuyenteId = ContribuyenteId,
            EmisorCuit = "20123456786",
            EmisorRazonSocial = "Demo SRL",
            PuntoVenta = 1,
            TipoComprobante = TipoComprobante.FacturaC,
            Numero = 42,
            FechaEmision = FechaEmision,
            Cae = "12345678901234",
            CaeVencimiento = Utc(2026, 12, 10),
            ReceptorDocumentoTipo = "CUIT",
            ReceptorDocumentoNumero = "20345678901",
            ReceptorRazonSocial = "Cliente Final",
            ReceptorCondicionIva = CondicionIva.ResponsableInscripto,
            ImporteNeto = 10000m,
            ImporteIva = 2100m,
            ImporteTotal = 12100m,
            Moneda = "ARS",
            Estado = EstadoComprobante.Emitido,
            Observaciones = "Observación de prueba"
        };
}

internal sealed class ConsultaComprobantesRepositorioFake : IComprobanteRepository
{
    public List<Comprobante> Comprobantes { get; } = [];

    public long? TotalPersonalizado { get; init; }

    public Guid? UltimoContribuyenteId { get; private set; }

    public Guid? UltimoId { get; private set; }

    public FiltroListadoComprobantes? UltimoFiltro { get; private set; }

    public int? UltimaPage { get; private set; }

    public int? UltimoPageSize { get; private set; }

    public int ConsultasListado { get; private set; }

    public int ConsultasDetalle { get; private set; }

    public Task<Comprobante?> GetByCaeAsync(
        string cae,
        CancellationToken cancellationToken = default)
        => Task.FromResult(Comprobantes.FirstOrDefault(c => c.Cae == cae));

    public Task<Comprobante?> GetByIdAsync(
        Guid contribuyenteId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        ConsultasDetalle++;
        UltimoContribuyenteId = contribuyenteId;
        UltimoId = id;

        return Task.FromResult(Comprobantes.FirstOrDefault(
            c => c.Id == id && c.ContribuyenteId == contribuyenteId));
    }

    public Task<Comprobante?> GetByNumeracionAsync(
        Guid contribuyenteId,
        int puntoVenta,
        TipoComprobante tipo,
        long numero,
        CancellationToken cancellationToken = default)
    {
        ConsultasDetalle++;
        UltimoContribuyenteId = contribuyenteId;

        return Task.FromResult(Comprobantes.FirstOrDefault(
            c => c.ContribuyenteId == contribuyenteId
                && c.PuntoVenta == puntoVenta
                && c.TipoComprobante == tipo
                && c.Numero == numero));
    }

    public Task<Comprobante?> GetUltimoEmitidoAsync(
        Guid contribuyenteId,
        int puntoVenta,
        TipoComprobante tipo,
        CancellationToken cancellationToken = default)
    {
        ConsultasDetalle++;
        UltimoContribuyenteId = contribuyenteId;

        return Task.FromResult(Comprobantes
            .Where(c => c.ContribuyenteId == contribuyenteId
                && c.PuntoVenta == puntoVenta
                && c.TipoComprobante == tipo
                && c.Estado == EstadoComprobante.Emitido)
            .OrderByDescending(c => c.Numero)
            .FirstOrDefault());
    }

    public Task<Comprobante?> GetByNumeracionAsync(
        Guid contribuyenteId,
        int puntoVenta,
        int tipoComprobante,
        long numero,
        CancellationToken cancellationToken = default)
    {
        ConsultasDetalle++;
        UltimoContribuyenteId = contribuyenteId;

        return Task.FromResult(Comprobantes.FirstOrDefault(c =>
            c.ContribuyenteId == contribuyenteId
            && c.PuntoVenta == puntoVenta
            && (int)c.TipoComprobante == tipoComprobante
            && c.Numero == numero));
    }

    public Task<Comprobante?> GetUltimoEmitidoAsync(
        Guid contribuyenteId,
        int puntoVenta,
        int tipoComprobante,
        CancellationToken cancellationToken = default)
    {
        ConsultasDetalle++;
        UltimoContribuyenteId = contribuyenteId;

        return Task.FromResult(Comprobantes
            .Where(c => c.ContribuyenteId == contribuyenteId
                && c.PuntoVenta == puntoVenta
                && (int)c.TipoComprobante == tipoComprobante)
            .OrderByDescending(c => c.Numero)
            .FirstOrDefault());
    }

    public Task<(IReadOnlyList<Comprobante> Items, long Total)> ListarAsync(
        Guid contribuyenteId,
        FiltroListadoComprobantes filtro,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ConsultasListado++;
        UltimoContribuyenteId = contribuyenteId;
        UltimoFiltro = filtro;
        UltimaPage = page;
        UltimoPageSize = pageSize;

        var coincidencias = Comprobantes
            .Where(c => c.ContribuyenteId == contribuyenteId)
            .Where(c => filtro.Desde is null || c.FechaEmision >= filtro.Desde)
            .Where(c => filtro.HastaExclusivo is null || c.FechaEmision < filtro.HastaExclusivo)
            .Where(c => filtro.Tipo is null || c.TipoComprobante == filtro.Tipo)
            .Where(c => filtro.Estado is null || c.Estado == filtro.Estado)
            .Where(c => filtro.PuntoVenta is null || c.PuntoVenta == filtro.PuntoVenta)
            .Where(c => filtro.ReceptorDocumentoNumero is null
                || c.ReceptorDocumentoNumero == filtro.ReceptorDocumentoNumero)
            .OrderByDescending(c => c.FechaEmision)
            .ThenByDescending(c => c.Numero)
            .ThenBy(c => c.Id)
            .ToList();

        var items = coincidencias
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Task.FromResult<(IReadOnlyList<Comprobante>, long)>(
            (items, TotalPersonalizado ?? coincidencias.Count));
    }

    public Task<Comprobante?> GetByIdempotencyKeyAsync(
        Guid contribuyenteId,
        string clave,
        CancellationToken cancellationToken = default)
        => Task.FromResult(Comprobantes.FirstOrDefault(
            c => c.ContribuyenteId == contribuyenteId && c.IdempotencyKey == clave));

    public Task AddAsync(Comprobante comprobante, CancellationToken cancellationToken = default)
    {
        Comprobantes.Add(comprobante);
        return Task.CompletedTask;
    }

    public Task<bool> AjustarSaldoAsync(
        Guid contribuyenteId,
        Guid originalId,
        decimal? saldoEsperado,
        decimal saldoNuevo,
        CancellationToken cancellationToken = default)
        => Task.FromResult(false);
}

internal sealed class ConsultaSecuenciasRepositorioFake : ISecuenciaRepository
{
    public long UltimoNumero { get; set; }

    public long MaximoUltimoNumero { get; set; }

    public Guid? UltimoContribuyenteId { get; private set; }

    public int? UltimoPuntoVenta { get; private set; }

    public TipoComprobante? UltimoTipo { get; private set; }

    public bool ConsultoMaximo { get; private set; }

    public int Consultas { get; private set; }

    public Task<long> ObtenerSiguienteNumeroAsync(
        Guid contribuyenteId,
        int puntoVenta,
        TipoComprobante tipo,
        CancellationToken ct = default)
        => throw new NotSupportedException("La consulta no asigna números.");

    public Task<long> ObtenerUltimoNumeroAsync(
        Guid contribuyenteId,
        int puntoVenta,
        TipoComprobante tipo,
        CancellationToken ct = default)
    {
        Consultas++;
        UltimoContribuyenteId = contribuyenteId;
        UltimoPuntoVenta = puntoVenta;
        UltimoTipo = tipo;
        ConsultoMaximo = false;

        return Task.FromResult(UltimoNumero);
    }

    public Task<long> ObtenerMaximoUltimoNumeroAsync(
        Guid contribuyenteId,
        TipoComprobante tipo,
        CancellationToken ct = default)
    {
        Consultas++;
        UltimoContribuyenteId = contribuyenteId;
        UltimoPuntoVenta = null;
        UltimoTipo = tipo;
        ConsultoMaximo = true;

        return Task.FromResult(MaximoUltimoNumero);
    }
}
