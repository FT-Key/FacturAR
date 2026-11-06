using System.Net;
using System.Net.Http.Json;
using FacturAR.Contracts.Comunes;
using FacturAR.Contracts.PuntosVenta;
using FacturAR.Domain.Entities;
using FacturAR.Domain.Enums;

namespace FacturAR.IntegrationTests;

public sealed class PuntoVentaEndpointTests
{
    [Fact]
    public async Task Listar_ConCookieAdministrador_DevuelvePuntosOrdenadosEIncluyeInactivos()
    {
        using var factory = new ComprobanteApiKeyFactory();
        factory.PuntosVenta.PuntosVenta.Add(new PuntoVenta
        {
            Id = Guid.NewGuid(),
            ContribuyenteId = ComprobanteApiKeyFactory.ContribuyenteId,
            Numero = 2,
            Descripcion = "Sucursal",
            Activo = true
        });
        factory.PuntosVenta.PuntosVenta.Add(new PuntoVenta
        {
            Id = Guid.NewGuid(),
            ContribuyenteId = ComprobanteApiKeyFactory.ContribuyenteId,
            Numero = 3,
            Descripcion = "Cerrado",
            Activo = false
        });
        using var client = ComprobantePortalTestHelpers.CrearCliente(factory);

        var cookie = await ComprobantePortalTestHelpers.IniciarSesionAsync(client);
        var response = await ComprobantePortalTestHelpers.ObtenerConCookieAsync(
            client,
            "/v1/puntos-venta",
            cookie);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var lista = await response.Content.ReadFromJsonAsync<PuntoVentaListResponse>();
        Assert.NotNull(lista);
        Assert.Equal(new[] { 1, 2, 3 }, lista!.Items.Select(punto => punto.Numero));

        var inactivo = Assert.Single(lista.Items, punto => punto.Numero == 3);
        Assert.False(inactivo.Activo);
        Assert.Equal("Cerrado", inactivo.Descripcion);
        Assert.True(Assert.Single(lista.Items, punto => punto.Numero == 1).Activo);
    }

    [Fact]
    public async Task Listar_ConCookieEmisor_Devuelve200()
    {
        using var factory = new ComprobanteApiKeyFactory();
        factory.AgregarUsuario(ComprobanteApiKeyFactory.EmailEmisor, RolUsuario.Emisor);
        using var client = ComprobantePortalTestHelpers.CrearCliente(factory);

        var cookie = await ComprobantePortalTestHelpers.IniciarSesionAsync(
            client,
            ComprobanteApiKeyFactory.EmailEmisor);
        var response = await ComprobantePortalTestHelpers.ObtenerConCookieAsync(
            client,
            "/v1/puntos-venta",
            cookie);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var lista = await response.Content.ReadFromJsonAsync<PuntoVentaListResponse>();
        Assert.NotNull(lista);
        Assert.Single(lista!.Items);
    }

    [Fact]
    public async Task Listar_NoExponePuntosDeVentaDeOtroContribuyente()
    {
        using var factory = new ComprobanteApiKeyFactory();
        var idPropio = factory.PuntosVenta.PuntosVenta
            .Single(punto => punto.ContribuyenteId == ComprobanteApiKeyFactory.ContribuyenteId)
            .Id;
        var idAjeno = Guid.NewGuid();
        factory.PuntosVenta.PuntosVenta.Add(new PuntoVenta
        {
            Id = idAjeno,
            ContribuyenteId = ComprobanteApiKeyFactory.ContribuyenteIdB,
            Numero = 9,
            Descripcion = "Ajena",
            Activo = true
        });
        using var client = ComprobantePortalTestHelpers.CrearCliente(factory);

        var cookie = await ComprobantePortalTestHelpers.IniciarSesionAsync(client);
        var response = await ComprobantePortalTestHelpers.ObtenerConCookieAsync(
            client,
            "/v1/puntos-venta",
            cookie);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var lista = await response.Content.ReadFromJsonAsync<PuntoVentaListResponse>();
        Assert.NotNull(lista);
        var item = Assert.Single(lista!.Items);
        Assert.Equal(idPropio, item.Id);
        Assert.DoesNotContain(lista.Items, punto => punto.Id == idAjeno);
    }

    [Fact]
    public async Task Listar_ConRolConsulta_Devuelve403SinPermisos()
    {
        using var factory = new ComprobanteApiKeyFactory();
        factory.AgregarUsuario(ComprobanteApiKeyFactory.EmailConsulta, RolUsuario.Consulta);
        using var client = ComprobantePortalTestHelpers.CrearCliente(factory);

        var cookie = await ComprobantePortalTestHelpers.IniciarSesionAsync(
            client,
            ComprobanteApiKeyFactory.EmailConsulta);
        var response = await ComprobantePortalTestHelpers.ObtenerConCookieAsync(
            client,
            "/v1/puntos-venta",
            cookie);

        await AssertErrorAsync(response, HttpStatusCode.Forbidden, "SIN_PERMISOS");
    }

    [Fact]
    public async Task Listar_SinSesion_Devuelve401()
    {
        using var factory = new ComprobanteApiKeyFactory();
        using var client = ComprobantePortalTestHelpers.CrearCliente(factory);

        var response = await client.GetAsync("/v1/puntos-venta");

        await AssertErrorAsync(response, HttpStatusCode.Unauthorized, "CREDENCIALES_INVALIDAS");
    }

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
