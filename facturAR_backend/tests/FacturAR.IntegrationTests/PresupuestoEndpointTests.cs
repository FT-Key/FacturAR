using System.Net;
using System.Net.Http.Json;
using FacturAR.Application.Comprobantes;
using FacturAR.Contracts.Comunes;
using FacturAR.Contracts.Comprobantes;
using FacturAR.Domain.Enums;

namespace FacturAR.IntegrationTests;

public sealed class PresupuestoEndpointTests
{
    [Fact]
    public async Task Presupuesto_ConCookieAdministrador_DevuelveImportesYNoPersiste()
    {
        using var factory = new ComprobanteApiKeyFactory();
        using var client = ComprobantePortalTestHelpers.CrearCliente(factory);

        var cookie = await ComprobantePortalTestHelpers.IniciarSesionAsync(client);

        var response = await ComprobantePortalTestHelpers.EnviarConCookieAsync(
            client,
            "/v1/comprobantes/presupuesto",
            FacturaC(),
            cookie);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var presupuesto = await response.Content.ReadFromJsonAsync<PresupuestoResponse>();
        Assert.NotNull(presupuesto);
        Assert.Equal(10000m, presupuesto!.ImporteNeto);
        Assert.Equal(0m, presupuesto.ImporteIva);
        Assert.Equal(10000m, presupuesto.ImporteTotal);
        Assert.Equal(10000m, Assert.Single(presupuesto.Items).Subtotal);

        Assert.Empty(factory.Comprobantes.Comprobantes);
        Assert.Equal(0, factory.Secuencias.Llamadas);
        Assert.Equal(0, factory.Transaccion.Ejecuciones);
        Assert.DoesNotContain(
            factory.Eventos.Eventos,
            evento => evento.Accion == EmitirComprobanteService.AccionEmitido);
    }

    [Fact]
    public async Task Presupuesto_FacturaADosPorMilConIva21_DevuelveNetoIvaYTotal()
    {
        using var factory = new ComprobanteApiKeyFactory();
        factory.AgregarUsuario(ComprobanteApiKeyFactory.EmailEmisor, RolUsuario.Emisor);
        using var client = ComprobantePortalTestHelpers.CrearCliente(factory);

        var cookie = await ComprobantePortalTestHelpers.IniciarSesionAsync(
            client,
            ComprobanteApiKeyFactory.EmailEmisor);

        var response = await ComprobantePortalTestHelpers.EnviarConCookieAsync(
            client,
            "/v1/comprobantes/presupuesto",
            FacturaA(),
            cookie);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var presupuesto = await response.Content.ReadFromJsonAsync<PresupuestoResponse>();
        Assert.NotNull(presupuesto);
        Assert.Equal(2000m, presupuesto!.ImporteNeto);
        Assert.Equal(420m, presupuesto.ImporteIva);
        Assert.Equal(2420m, presupuesto.ImporteTotal);

        var item = Assert.Single(presupuesto.Items);
        Assert.Equal("Servicio", item.Descripcion);
        Assert.Equal(2m, item.Cantidad);
        Assert.Equal(1000m, item.PrecioUnitario);
        Assert.Equal(21m, item.AlicuotaIva);
        Assert.Equal(2000m, item.Subtotal);

        Assert.Empty(factory.Comprobantes.Comprobantes);
        Assert.Equal(0, factory.Secuencias.Llamadas);
        Assert.Equal(0, factory.Transaccion.Ejecuciones);
    }

    [Fact]
    public async Task Presupuesto_ConCantidadCero_Devuelve400ValidacionYSinEscrituras()
    {
        using var factory = new ComprobanteApiKeyFactory();
        using var client = ComprobantePortalTestHelpers.CrearCliente(factory);

        var cookie = await ComprobantePortalTestHelpers.IniciarSesionAsync(client);

        var response = await ComprobantePortalTestHelpers.EnviarConCookieAsync(
            client,
            "/v1/comprobantes/presupuesto",
            FacturaC(cantidad: 0m),
            cookie);

        await AssertErrorAsync(response, HttpStatusCode.BadRequest, "VALIDACION");
        Assert.Empty(factory.Comprobantes.Comprobantes);
        Assert.Equal(0, factory.Secuencias.Llamadas);
        Assert.Equal(0, factory.Transaccion.Ejecuciones);
    }

    [Fact]
    public async Task Presupuesto_ConDescripcionVacia_Devuelve400ValidacionYSinEscrituras()
    {
        using var factory = new ComprobanteApiKeyFactory();
        using var client = ComprobantePortalTestHelpers.CrearCliente(factory);

        var cookie = await ComprobantePortalTestHelpers.IniciarSesionAsync(client);

        var response = await ComprobantePortalTestHelpers.EnviarConCookieAsync(
            client,
            "/v1/comprobantes/presupuesto",
            FacturaC(descripcion: string.Empty),
            cookie);

        await AssertErrorAsync(response, HttpStatusCode.BadRequest, "VALIDACION");
        Assert.Empty(factory.Comprobantes.Comprobantes);
        Assert.Equal(0, factory.Secuencias.Llamadas);
    }

    [Fact]
    public async Task Presupuesto_ConRolConsulta_Devuelve403SinPermisos()
    {
        using var factory = new ComprobanteApiKeyFactory();
        factory.AgregarUsuario(ComprobanteApiKeyFactory.EmailConsulta, RolUsuario.Consulta);
        using var client = ComprobantePortalTestHelpers.CrearCliente(factory);

        var cookie = await ComprobantePortalTestHelpers.IniciarSesionAsync(
            client,
            ComprobanteApiKeyFactory.EmailConsulta);

        var response = await ComprobantePortalTestHelpers.EnviarConCookieAsync(
            client,
            "/v1/comprobantes/presupuesto",
            FacturaC(),
            cookie);

        await AssertErrorAsync(response, HttpStatusCode.Forbidden, "SIN_PERMISOS");
    }

    [Fact]
    public async Task Presupuesto_SinSesion_Devuelve401()
    {
        using var factory = new ComprobanteApiKeyFactory();
        using var client = ComprobantePortalTestHelpers.CrearCliente(factory);

        var response = await ComprobantePortalTestHelpers.EnviarSinSesionAsync(
            client,
            "/v1/comprobantes/presupuesto",
            FacturaC());

        await AssertErrorAsync(response, HttpStatusCode.Unauthorized, "CREDENCIALES_INVALIDAS");
    }

    private static EmitirComprobanteRequest FacturaA()
        => new(
            (int)TipoComprobante.FacturaA,
            1,
            null,
            new ReceptorComprobanteRequest(
                "CUIT",
                ComprobanteApiKeyFactory.CuitReceptor,
                "Cliente S.A.",
                (int)CondicionIva.ResponsableInscripto),
            [new ItemComprobanteRequest("Servicio", 2m, 1000m, 21m)],
            null,
            null);

    private static EmitirComprobanteRequest FacturaC(
        decimal cantidad = 1m,
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
            [new ItemComprobanteRequest(descripcion, cantidad, 10000m, 21m)],
            null,
            null);

    private static async Task<ErrorResponse> AssertErrorAsync(
        HttpResponseMessage response,
        HttpStatusCode status,
        string codigo)
    {
        Assert.Equal(status, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal(codigo, error.Codigo);
        return error;
    }
}
