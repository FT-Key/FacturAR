using System.Net;

namespace FacturAR.IntegrationTests;

public sealed class PortalLandingTests
{
    [Fact]
    public async Task Raiz_SinSesion_RenderizaLaLandingPublica()
    {
        using var factory = new PortalWebApplicationFactory();
        using var client = factory.CrearCliente();

        var response = await client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.Contains("lang=\"es-AR\"", html);
        Assert.Contains("<h1", html);
        Assert.Contains("Solicitar alta", html);
        Assert.Contains("Cómo funciona", html);
    }

    [Fact]
    public async Task Raiz_IncluyeElSeoTecnicoYElCanonicalConfigurado()
    {
        using var factory = new PortalWebApplicationFactory();
        using var client = factory.CrearCliente();

        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/"));

        Assert.Contains("<title>FacturAR — Facturación electrónica argentina simulada</title>", html);
        Assert.Contains("<html lang=\"es-AR\">", html);
        Assert.Contains("name=\"description\"", html);
        Assert.Contains("rel=\"canonical\" href=\"http://localhost/\"", html);
        Assert.Contains("property=\"og:type\" content=\"website\"", html);
        Assert.Contains("property=\"og:site_name\" content=\"FacturAR\"", html);
        Assert.Contains("property=\"og:url\" content=\"http://localhost/\"", html);
        Assert.Contains("name=\"twitter:card\" content=\"summary_large_image\"", html);
        Assert.Contains("\"Organization\"", html);
        Assert.Contains("\"WebSite\"", html);
        Assert.Contains("\"FAQPage\"", html);
        Assert.Contains("\"inLanguage\":\"es-AR\"", html);
    }

    [Fact]
    public async Task Raiz_MuestraElEjemploCurlConLaUrlConfiguradaDeLaApi()
    {
        using var factory = new PortalWebApplicationFactory()
            .Configurar("Api:BaseUrl", "https://api.facturar.test/");
        using var client = factory.CrearCliente();

        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/"));

        Assert.Contains("https://api.facturar.test/v1/comprobantes", html);
        Assert.DoesNotContain("localhost:5171", html);
    }

    [Fact]
    public async Task Raiz_TieneUnSoloH1()
    {
        using var factory = new PortalWebApplicationFactory();
        using var client = factory.CrearCliente();

        var html = await client.GetStringAsync("/");

        Assert.Equal(1, Contar(html, "<h1"));
    }

    [Fact]
    public async Task Inicio_RedirigeDeFormaPermanenteARaiz()
    {
        using var factory = new PortalWebApplicationFactory();
        using var client = factory.CrearCliente();

        var response = await client.GetAsync("/inicio");

        Assert.Equal(HttpStatusCode.MovedPermanently, response.StatusCode);
        Assert.Equal("/", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Dashboard_SinSesion_RedirigeAlLogin()
    {
        using var factory = new PortalWebApplicationFactory();
        using var client = factory.CrearCliente();

        var response = await client.GetAsync("/dashboard");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.EndsWith("/login", response.Headers.Location?.OriginalString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Login_EsPublicoYNoSeIndexa()
    {
        using var factory = new PortalWebApplicationFactory();
        using var client = factory.CrearCliente();

        var html = await client.GetStringAsync("/login");

        Assert.Contains("content=\"noindex", html);
    }

    [Fact]
    public async Task AdminLogin_EsPublicoYNoSeIndexa()
    {
        using var factory = new PortalWebApplicationFactory();
        using var client = factory.CrearCliente();

        var html = await client.GetStringAsync("/admin/login");

        Assert.Contains("content=\"noindex", html);
    }

    [Fact]
    public async Task Activar_EsPublicaYNoSeIndexa()
    {
        using var factory = new PortalWebApplicationFactory();
        using var client = factory.CrearCliente();

        var html = await client.GetStringAsync("/activar");

        Assert.Contains("content=\"noindex", html);
    }

    [Fact]
    public async Task Raiz_NoPublicaNoindex()
    {
        using var factory = new PortalWebApplicationFactory();
        using var client = factory.CrearCliente();

        var html = await client.GetStringAsync("/");

        Assert.DoesNotContain("content=\"noindex", html);
    }

    [Fact]
    public async Task RobotsTxt_PermiteRutasPublicasYBloqueaLasPrivadas()
    {
        using var factory = new PortalWebApplicationFactory();
        using var client = factory.CrearCliente();

        var response = await client.GetAsync("/robots.txt");
        var contenido = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("User-agent: *", contenido);
        Assert.Contains("Allow: /solicitar-alta", contenido);
        Assert.Contains("Allow: /verificar", contenido);
        Assert.Contains("Disallow: /dashboard", contenido);
        Assert.Contains("Disallow: /login", contenido);
        Assert.Contains("Sitemap: http://localhost/sitemap.xml", contenido);
    }

    [Fact]
    public async Task SitemapXml_ListaSoloLasRutasPublicas()
    {
        using var factory = new PortalWebApplicationFactory();
        using var client = factory.CrearCliente();

        var response = await client.GetAsync("/sitemap.xml");
        var contenido = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/xml", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("<loc>http://localhost/</loc>", contenido);
        Assert.Contains("<loc>http://localhost/solicitar-alta</loc>", contenido);
        Assert.Contains("<loc>http://localhost/verificar</loc>", contenido);
        Assert.DoesNotContain("/dashboard", contenido);
    }

    private static int Contar(string texto, string fragmento)
    {
        var total = 0;
        var indice = 0;
        while ((indice = texto.IndexOf(fragmento, indice, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            total++;
            indice += fragmento.Length;
        }

        return total;
    }
}
