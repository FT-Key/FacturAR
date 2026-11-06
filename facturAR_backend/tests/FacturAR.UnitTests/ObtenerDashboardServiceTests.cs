using System.Text.Json;
using FacturAR.Application.Comprobantes;
using FacturAR.Contracts.Comprobantes;
using FacturAR.Domain.Entities;
using FacturAR.Domain.Enums;
using FacturAR.Domain.Exceptions;

namespace FacturAR.UnitTests;

public sealed class ObtenerDashboardServiceTests
{
    private static readonly Guid ContribuyenteId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTime FechaOctubre = new(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ObtenerAsync_ConMesValido_UsaElPeriodoDelMesEnUtc()
    {
        var servicio = CrearServicio();

        var dashboard = await servicio.ObtenerAsync(ContribuyenteId, "2026-10");

        Assert.Equal(Utc(2026, 10, 1), dashboard.Periodo.Desde);
        Assert.Equal(Utc(2026, 11, 1), dashboard.Periodo.Hasta);
        Assert.Equal(DateTimeKind.Utc, dashboard.Periodo.Desde.Kind);
        Assert.Equal(DateTimeKind.Utc, dashboard.Periodo.Hasta.Kind);
    }

    [Fact]
    public async Task ObtenerAsync_SinMes_UsaElMesUtcActualDelTimeProvider()
    {
        var timeProvider = new TimeProviderFijo(
            new DateTimeOffset(2026, 7, 15, 10, 30, 0, TimeSpan.Zero));
        var servicio = CrearServicio(timeProvider: timeProvider);

        var dashboard = await servicio.ObtenerAsync(ContribuyenteId, null);

        Assert.Equal(Utc(2026, 7, 1), dashboard.Periodo.Desde);
        Assert.Equal(Utc(2026, 8, 1), dashboard.Periodo.Hasta);
    }

    [Fact]
    public async Task ObtenerAsync_ConDiciembre_ElPeriodoTerminaEnEneroDelAnioSiguiente()
    {
        var servicio = CrearServicio();

        var dashboard = await servicio.ObtenerAsync(ContribuyenteId, "2026-12");

        Assert.Equal(Utc(2026, 12, 1), dashboard.Periodo.Desde);
        Assert.Equal(Utc(2027, 1, 1), dashboard.Periodo.Hasta);
    }

    [Theory]
    [InlineData("2026-13")]
    [InlineData("2026-1")]
    [InlineData("10-2026")]
    [InlineData("texto")]
    [InlineData("2026-10-01")]
    public async Task ObtenerAsync_ConMesInvalido_LanzaValidacionSinConsultarRepositorios(string mes)
    {
        var repositorio = new ConsultaComprobantesRepositorioFake();
        var secuencias = new ConsultaSecuenciasRepositorioFake();
        var servicio = CrearServicio(repositorio, secuencias);

        await Assert.ThrowsAsync<ComprobanteValidationException>(
            () => servicio.ObtenerAsync(ContribuyenteId, mes));

        Assert.Equal(0, repositorio.ConsultasMetricas);
        Assert.Equal(0, repositorio.ConsultasListado);
        Assert.Equal(0, secuencias.Consultas);
    }

    [Fact]
    public async Task ObtenerAsync_PropagaElTenantYElRangoYUsaElResultadoDeMetricas()
    {
        var repositorio = new ConsultaComprobantesRepositorioFake
        {
            MetricasPersonalizadas = new MetricasDashboard(7, 1234.56m)
        };
        var servicio = CrearServicio(repositorio);

        var dashboard = await servicio.ObtenerAsync(ContribuyenteId, "2026-10");

        Assert.Equal(ContribuyenteId, repositorio.UltimoContribuyenteMetricas);
        Assert.Equal(Utc(2026, 10, 1), repositorio.UltimoDesdeMetricas);
        Assert.Equal(Utc(2026, 11, 1), repositorio.UltimoHastaMetricas);
        Assert.Equal(7, dashboard.CantidadComprobantes);
        Assert.Equal(1234.56m, dashboard.ImporteTotal);
    }

    [Fact]
    public async Task ObtenerAsync_SinComprobantesEmitidos_DevuelveUltimoCaeNulo()
    {
        var repositorio = new ConsultaComprobantesRepositorioFake();
        repositorio.Comprobantes.Add(NuevoComprobante(
            numero: 1,
            fecha: FechaOctubre,
            estado: EstadoComprobante.Anulado,
            cae: "99999999999999"));
        var servicio = CrearServicio(repositorio);

        var dashboard = await servicio.ObtenerAsync(ContribuyenteId, "2026-10");

        Assert.Null(dashboard.UltimoCae);
    }

    [Fact]
    public async Task ObtenerAsync_ConVariosEmitidos_DevuelveElCaeDelMasReciente()
    {
        var repositorio = new ConsultaComprobantesRepositorioFake();
        repositorio.Comprobantes.Add(NuevoComprobante(
            numero: 1,
            fecha: FechaOctubre,
            cae: "11111111111111"));
        repositorio.Comprobantes.Add(NuevoComprobante(
            numero: 2,
            fecha: FechaOctubre.AddDays(2),
            cae: "22222222222222"));
        repositorio.Comprobantes.Add(NuevoComprobante(
            numero: 3,
            fecha: FechaOctubre.AddDays(4),
            estado: EstadoComprobante.Anulado,
            cae: "33333333333333"));
        var servicio = CrearServicio(repositorio);

        var dashboard = await servicio.ObtenerAsync(ContribuyenteId, "2026-10");

        Assert.Equal("22222222222222", dashboard.UltimoCae);
    }

    [Fact]
    public async Task ObtenerAsync_DevuelveElMaximoNumeroAutorizadoDeSecuencias()
    {
        var secuencias = new ConsultaSecuenciasRepositorioFake { MaximoUltimoNumero = 88 };
        var servicio = CrearServicio(secuencias: secuencias);

        var dashboard = await servicio.ObtenerAsync(ContribuyenteId, "2026-10");

        Assert.Equal(88, dashboard.UltimoNumeroAutorizado);
        Assert.Equal(ContribuyenteId, secuencias.UltimoContribuyenteId);
    }

    [Fact]
    public async Task ObtenerAsync_DevuelveLosCincoUltimosEnElOrdenDelListado()
    {
        var repositorio = new ConsultaComprobantesRepositorioFake();
        for (var indice = 1; indice <= 6; indice++)
        {
            repositorio.Comprobantes.Add(NuevoComprobante(
                numero: indice,
                fecha: FechaOctubre.AddDays(indice)));
        }

        var servicio = CrearServicio(repositorio);

        var dashboard = await servicio.ObtenerAsync(ContribuyenteId, "2026-10");

        Assert.Equal(5, dashboard.UltimosComprobantes.Count);
        Assert.Equal([6L, 5L, 4L, 3L, 2L], dashboard.UltimosComprobantes.Select(c => c.Numero).ToArray());

        var primero = dashboard.UltimosComprobantes[0];
        Assert.Equal((int)TipoComprobante.FacturaC, primero.TipoComprobante);
        Assert.Equal("Factura C", primero.TipoComprobanteDescripcion);
        Assert.Equal(1, primero.PuntoVenta);
        Assert.Equal("12345678901234", primero.Cae);
        Assert.Equal("Cliente Final", primero.ReceptorRazonSocial);
        Assert.Equal("20345678901", primero.ReceptorDocumentoNumero);
        Assert.Equal(12100m, primero.ImporteTotal);
        Assert.Equal("ARS", primero.Moneda);
        Assert.Equal(nameof(EstadoComprobante.Emitido), primero.Estado);
    }

    [Fact]
    public void DashboardResponse_NoExponeCamposInternos()
    {
        Assert.DoesNotContain(
            "ContribuyenteId",
            typeof(DashboardResponse).GetProperties().Select(propiedad => propiedad.Name));
        Assert.DoesNotContain(
            "ContribuyenteId",
            typeof(ComprobanteResumenResponse).GetProperties().Select(propiedad => propiedad.Name));

        var dashboard = new DashboardResponse(
            new PeriodoDashboard(Utc(2026, 10, 1), Utc(2026, 11, 1)),
            1,
            12100m,
            "12345678901234",
            42,
            [NuevoResumen()]);

        var json = JsonSerializer.Serialize(
            dashboard,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        Assert.DoesNotContain("contribuyenteId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("idempotencyKey", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hash", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("apikey", json, StringComparison.OrdinalIgnoreCase);
    }

    private static ObtenerDashboardService CrearServicio(
        ConsultaComprobantesRepositorioFake? repositorio = null,
        ConsultaSecuenciasRepositorioFake? secuencias = null,
        TimeProvider? timeProvider = null)
    {
        var comprobantes = repositorio ?? new ConsultaComprobantesRepositorioFake();

        return new ObtenerDashboardService(
            comprobantes,
            new ListarComprobantesService(comprobantes),
            secuencias ?? new ConsultaSecuenciasRepositorioFake(),
            timeProvider ?? new TimeProviderFijo(new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero)));
    }

    private static Comprobante NuevoComprobante(
        long numero,
        DateTime fecha,
        EstadoComprobante estado = EstadoComprobante.Emitido,
        string cae = "12345678901234")
        => new()
        {
            ContribuyenteId = ContribuyenteId,
            EmisorCuit = "20123456786",
            EmisorRazonSocial = "Demo SRL",
            PuntoVenta = 1,
            TipoComprobante = TipoComprobante.FacturaC,
            Numero = numero,
            FechaEmision = fecha,
            Cae = cae,
            CaeVencimiento = fecha.Date.AddDays(10),
            ReceptorDocumentoTipo = "CUIT",
            ReceptorDocumentoNumero = "20345678901",
            ReceptorRazonSocial = "Cliente Final",
            ReceptorCondicionIva = CondicionIva.ResponsableInscripto,
            ImporteNeto = 10000m,
            ImporteIva = 2100m,
            ImporteTotal = 12100m,
            Moneda = "ARS",
            Estado = estado
        };

    private static ComprobanteResumenResponse NuevoResumen()
        => new(
            Guid.NewGuid(),
            (int)TipoComprobante.FacturaC,
            "Factura C",
            1,
            42,
            FechaOctubre,
            "12345678901234",
            "Cliente Final",
            "20345678901",
            12100m,
            "ARS",
            nameof(EstadoComprobante.Emitido));

    private static DateTime Utc(int anio, int mes, int dia)
        => new(anio, mes, dia, 0, 0, 0, DateTimeKind.Utc);
}

internal sealed class TimeProviderFijo(DateTimeOffset ahora) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => ahora;
}
