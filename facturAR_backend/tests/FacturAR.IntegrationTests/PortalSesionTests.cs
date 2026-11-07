using System.Net;

namespace FacturAR.IntegrationTests;

public sealed class PortalSesionTests
{
    private const string CookieEmpresa = ".FacturAR.Contribuyente=sesion";
    private const string CookieAdmin = ".FacturAR.Admin=sesion";

    [Fact]
    public async Task Login_ConSesionDeEmpresa_RedirigeAlDashboard()
    {
        using var factory = new PortalSesionWebApplicationFactory();
        using var client = factory.CrearCliente();
        client.DefaultRequestHeaders.Add("Cookie", CookieEmpresa);

        var response = await client.GetAsync("/login");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/dashboard", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Login_ConSesionGlobal_RedirigeALaConsola()
    {
        using var factory = new PortalSesionWebApplicationFactory();
        using var client = factory.CrearCliente();
        client.DefaultRequestHeaders.Add("Cookie", CookieAdmin);

        var response = await client.GetAsync("/login");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/admin", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task AdminLogin_ConSesionDeEmpresa_RedirigeAlDashboard()
    {
        using var factory = new PortalSesionWebApplicationFactory();
        using var client = factory.CrearCliente();
        client.DefaultRequestHeaders.Add("Cookie", CookieEmpresa);

        var response = await client.GetAsync("/admin/login");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/dashboard", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Landing_ConSesionDeEmpresa_MuestraElPanelYNoOfreceIngresar()
    {
        using var factory = new PortalSesionWebApplicationFactory();
        using var client = factory.CrearCliente();
        client.DefaultRequestHeaders.Add("Cookie", CookieEmpresa);

        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/"));

        Assert.Contains("Ir al panel", html);
        Assert.Contains("Cerrar sesión", html);
        Assert.DoesNotContain(">Ingresar<", html);
        Assert.Contains("data-user-menu", html);
        Assert.Contains("user-menu__trigger", html);
        Assert.Contains("user-menu__list", html);
    }

    [Fact]
    public async Task Landing_ConSesionGlobal_MuestraLaConsolaYNoOfreceIngresar()
    {
        using var factory = new PortalSesionWebApplicationFactory();
        using var client = factory.CrearCliente();
        client.DefaultRequestHeaders.Add("Cookie", CookieAdmin);

        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/"));

        Assert.Contains("Consola global", html);
        Assert.Contains("Cerrar sesión", html);
        Assert.DoesNotContain(">Ingresar<", html);
    }

    [Fact]
    public async Task Dashboard_ConTemaOscuro_RenderizaElTemaEnElDocumento()
    {
        using var factory = new PortalSesionWebApplicationFactory();
        using var client = factory.CrearCliente();
        client.DefaultRequestHeaders.Add("Cookie", $"{CookieEmpresa}; facturar-tema=oscuro");

        var html = await client.GetStringAsync("/dashboard");

        Assert.Contains("data-theme=\"dark\"", html);
        Assert.Contains("data-bs-theme=\"dark\"", html);
        Assert.Contains("data-theme-preference=\"oscuro\"", html);
    }

    [Fact]
    public async Task Logout_EliminaLasCookiesDeEmpresaYGlobal()
    {
        using var factory = new PortalSesionWebApplicationFactory();
        using var client = factory.CrearCliente();
        client.DefaultRequestHeaders.Add("Cookie", $"{CookieEmpresa}; {CookieAdmin}");

        var response = await client.GetAsync("/logout");
        var cookies = response.Headers.TryGetValues("Set-Cookie", out var valores)
            ? valores.ToList()
            : [];

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains(cookies, cookie =>
            cookie.StartsWith(".FacturAR.Contribuyente=;", StringComparison.Ordinal)
            && cookie.Contains("expires=", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(cookies, cookie =>
            cookie.StartsWith(".FacturAR.Admin=;", StringComparison.Ordinal)
            && cookie.Contains("expires=", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Landing_ConApiCaida_RespondeSinSesionYNoRompe()
    {
        using var factory = new PortalSesionWebApplicationFactory();
        factory.Api.Disponible = false;
        using var client = factory.CrearCliente();
        client.DefaultRequestHeaders.Add("Cookie", CookieEmpresa);

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Ingresar", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
    }
}
