using FacturAR.Application.Abstractions;
using FacturAR.Application.Parametros;

namespace FacturAR.UnitTests;

public sealed class ParametrosServiceTests
{
    private static readonly Guid ContribuyenteId = Guid.NewGuid();

    [Fact]
    public async Task CP1801_SinConfiguracion_DevuelveLosValoresDelMvp()
    {
        var servicio = Crear();

        Assert.Equal(10, await servicio.ObtenerDiasVencimientoCaeAsync(null));
        Assert.Equal(new decimal[] { 0m, 5m, 10.5m, 21m, 27m }, await servicio.ObtenerAlicuotasPermitidasAsync(null));
        Assert.Equal(1, await servicio.ObtenerPuntoVentaPorDefectoAsync(null));
        Assert.Equal("ARS", await servicio.ObtenerMonedaAsync(null));
        Assert.Equal(20, await servicio.ObtenerPaginaPorDefectoAsync(null));
        Assert.Equal(100, await servicio.ObtenerPaginaMaximaAsync(null));
    }

    [Fact]
    public async Task CP1802_ConfiguracionGanaSobreElDefault()
    {
        var servicio = Crear(new ParametrosFacturacion { Moneda = "USD" });

        Assert.Equal("USD", await servicio.ObtenerMonedaAsync(null));
    }

    [Fact]
    public async Task CP1803_OverridePorContribuyenteGanaSoloParaEseContribuyente()
    {
        var repositorio = new ParametrosRepositoryFake();
        repositorio.Valores[(ContribuyenteId, "Moneda")] = "USD";
        var servicio = Crear(repositorio: repositorio);

        Assert.Equal("USD", await servicio.ObtenerMonedaAsync(ContribuyenteId));
        Assert.Equal("ARS", await servicio.ObtenerMonedaAsync(Guid.NewGuid()));
        Assert.Equal("ARS", await servicio.ObtenerMonedaAsync(null));
    }

    [Fact]
    public async Task OverridePorContribuyente_GanaSobreLaConfiguracionGlobal()
    {
        var repositorio = new ParametrosRepositoryFake();
        repositorio.Valores[(ContribuyenteId, "Moneda")] = "USD";
        var servicio = Crear(new ParametrosFacturacion { Moneda = "EUR" }, repositorio);

        Assert.Equal("USD", await servicio.ObtenerMonedaAsync(ContribuyenteId));
        Assert.Equal("EUR", await servicio.ObtenerMonedaAsync(Guid.NewGuid()));
        Assert.Equal("EUR", await servicio.ObtenerMonedaAsync(null));
    }

    [Fact]
    public async Task ConfiguracionInvalida_UsaElValorPredeterminado()
    {
        var servicio = Crear(new ParametrosFacturacion
        {
            DiasVencimientoCae = 0,
            PuntoVentaPorDefecto = -1,
            PaginaPorDefecto = 0,
            PaginaMaxima = 0,
            Moneda = "   ",
            AlicuotasPermitidas = []
        });

        Assert.Equal(10, await servicio.ObtenerDiasVencimientoCaeAsync(null));
        Assert.Equal(1, await servicio.ObtenerPuntoVentaPorDefectoAsync(null));
        Assert.Equal(20, await servicio.ObtenerPaginaPorDefectoAsync(null));
        Assert.Equal(100, await servicio.ObtenerPaginaMaximaAsync(null));
        Assert.Equal("ARS", await servicio.ObtenerMonedaAsync(null));
        Assert.Equal(
            new decimal[] { 0m, 5m, 10.5m, 21m, 27m },
            await servicio.ObtenerAlicuotasPermitidasAsync(null));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("21;abc")]
    [InlineData("abc;21")]
    public async Task OverrideDeAlicuotasConValorNoNumerico_UsaElValorPredeterminado(string valor)
    {
        var repositorio = new ParametrosRepositoryFake();
        repositorio.Valores[(ContribuyenteId, "AlicuotasPermitidas")] = valor;
        var servicio = Crear(repositorio: repositorio);

        Assert.Equal(
            new decimal[] { 0m, 5m, 10.5m, 21m, 27m },
            await servicio.ObtenerAlicuotasPermitidasAsync(ContribuyenteId));
    }

    [Fact]
    public async Task CP1805_OverrideDeDiasVencimiento_CambiaSoloAlContribuyente()
    {
        var repositorio = new ParametrosRepositoryFake();
        repositorio.Valores[(ContribuyenteId, "DiasVencimientoCae")] = "5";
        var servicio = Crear(repositorio: repositorio);

        Assert.Equal(5, await servicio.ObtenerDiasVencimientoCaeAsync(ContribuyenteId));
        Assert.Equal(10, await servicio.ObtenerDiasVencimientoCaeAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task OverrideDeAlicuotas_SeparaPorPuntoYComa()
    {
        var repositorio = new ParametrosRepositoryFake();
        repositorio.Valores[(ContribuyenteId, "AlicuotasPermitidas")] = "0;21";
        var servicio = Crear(repositorio: repositorio);

        Assert.Equal(new decimal[] { 0m, 21m }, await servicio.ObtenerAlicuotasPermitidasAsync(ContribuyenteId));
    }

    [Theory]
    [InlineData("no-numerico")]
    [InlineData("0")]
    [InlineData("-3")]
    public async Task OverrideNumericoInvalido_UsaElValorDelMvp(string valor)
    {
        var repositorio = new ParametrosRepositoryFake();
        repositorio.Valores[(ContribuyenteId, "PaginaMaxima")] = valor;
        var servicio = Crear(repositorio: repositorio);

        Assert.Equal(100, await servicio.ObtenerPaginaMaximaAsync(ContribuyenteId));
    }

    [Fact]
    public async Task OverrideDeMonedaVacio_UsaElValorGlobal()
    {
        var repositorio = new ParametrosRepositoryFake();
        repositorio.Valores[(ContribuyenteId, "Moneda")] = "   ";
        var servicio = Crear(repositorio: repositorio);

        Assert.Equal("ARS", await servicio.ObtenerMonedaAsync(ContribuyenteId));
    }

    [Fact]
    public async Task ObtenerAsync_ClaveDesconocida_DevuelveNull()
    {
        var servicio = Crear();

        Assert.Null(await servicio.ObtenerAsync("Inexistente", ContribuyenteId));
    }

    [Fact]
    public async Task ObtenerAsync_ClaveConocida_DevuelveElValorSerializado()
    {
        var servicio = Crear(new ParametrosFacturacion { PaginaPorDefecto = 50 });

        Assert.Equal("50", await servicio.ObtenerAsync("PaginaPorDefecto", null));
    }

    private static ParametrosService Crear(
        ParametrosFacturacion? parametros = null,
        ParametrosRepositoryFake? repositorio = null)
        => new(repositorio ?? new ParametrosRepositoryFake(), parametros ?? new ParametrosFacturacion());

    private sealed class ParametrosRepositoryFake : IParametrosRepository
    {
        public Dictionary<(Guid ContribuyenteId, string Clave), string> Valores { get; } = [];

        public Task<string?> ObtenerValorAsync(Guid contribuyenteId, string clave, CancellationToken ct = default)
            => Task.FromResult(
                Valores.TryGetValue((contribuyenteId, clave), out var valor) ? valor : null);
    }
}
