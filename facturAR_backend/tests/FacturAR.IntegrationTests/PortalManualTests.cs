using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FacturAR.IntegrationTests;

public sealed partial class PortalManualTests
{
    [Fact]
    public async Task Manual_SinSesion_Responde200ConTituloYToc()
    {
        using var factory = new PortalWebApplicationFactory();
        using var client = factory.CrearCliente();

        var response = await client.GetAsync("/manual");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("<h1 id=\"manual-titulo\" class=\"manual-header__title\">Manual de integración</h1>", html);
        Assert.Contains("class=\"manual-toc\"", html);
        Assert.Contains("class=\"skip-link\" href=\"#contenido\"", html);
        Assert.Contains("CAE es simulado y los comprobantes no tienen validez fiscal", html);
    }

    [Fact]
    public async Task Manual_TablaDeContenidosYAnclas_SincronizadasConMarkdownFuente()
    {
        var rutaMarkdown = ObtenerRutaDocs("manual-integracion.md");
        var lineas = await File.ReadAllLinesAsync(rutaMarkdown);
        var encabezadosEsperados = ExtraerEncabezadosMarkdown(lineas);

        using var factory = new PortalWebApplicationFactory();
        using var client = factory.CrearCliente();

        var response = await client.GetAsync("/manual");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEmpty(encabezadosEsperados);

        var enlacesToc = TocLinkRegex().Matches(html)
            .Select(m => (Id: m.Groups[1].Value, Texto: WebUtility.HtmlDecode(m.Groups[2].Value).Trim()))
            .ToList();

        Assert.Equal(encabezadosEsperados.Count, enlacesToc.Count);

        var idsUnicos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < encabezadosEsperados.Count; i++)
        {
            Assert.Equal(encabezadosEsperados[i], enlacesToc[i].Texto);
            Assert.True(idsUnicos.Add(enlacesToc[i].Id), $"El id '{enlacesToc[i].Id}' está duplicado en la TOC.");

            var patronAncla = $"id=\"{Regex.Escape(enlacesToc[i].Id)}\"";
            var coincidencias = Regex.Matches(html, patronAncla);
            Assert.Single(coincidencias);
        }
    }

    [Fact]
    public async Task Manual_IncluyeEjemplosDeCodigoYBotonesDeCopiadoAccesibles()
    {
        using var factory = new PortalWebApplicationFactory();
        using var client = factory.CrearCliente();

        var response = await client.GetAsync("/manual");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("class=\"language-bash\"", html);
        Assert.Contains("class=\"language-javascript\"", html);
        Assert.Contains("class=\"language-csharp\"", html);
        Assert.Contains("data-copiar-manual", html);
        Assert.Contains("aria-live=\"polite\" data-copiar-estado=\"manual-codigo-1\"", html);
        Assert.Contains("/js/manual.js", html);
    }

    [Fact]
    public async Task Manual_MuestraColeccionPostmanValida()
    {
        using var factory = new PortalWebApplicationFactory();
        using var client = factory.CrearCliente();

        var response = await client.GetAsync("/manual");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("openapi", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("href=\"/downloads/FacturAR.postman_collection.json\"", html);

        var postmanResponse = await client.GetAsync("/downloads/FacturAR.postman_collection.json");
        var postmanContenido = await postmanResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, postmanResponse.StatusCode);
        Assert.Equal("application/json", postmanResponse.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", postmanResponse.Content.Headers.ContentDisposition?.DispositionType);
        Assert.True(postmanResponse.Headers.CacheControl?.Public);
        Assert.Equal(TimeSpan.FromHours(1), postmanResponse.Headers.CacheControl?.MaxAge);

        using var json = JsonDocument.Parse(postmanContenido);
        Assert.Equal("FacturAR API", json.RootElement.GetProperty("info").GetProperty("name").GetString());
    }

    [Fact]
    public async Task Manual_NoExponeRutasDeAdministracionNiDetallesInternos()
    {
        using var factory = new PortalWebApplicationFactory()
            .ConEntorno("Production");
        using var client = factory.CrearCliente();

        var response = await client.GetAsync("/manual");
        var html = await response.Content.ReadAsStringAsync();
        var markdown = await File.ReadAllTextAsync(ObtenerRutaDocs("manual-integracion.md"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("data-openapi-link", html);
        Assert.DoesNotContain("data-openapi-nota", html);

        foreach (var contenido in new[] { html, markdown })
        {
            Assert.DoesNotContain("/v1/admin", contenido);
            Assert.DoesNotContain("/admin/solicitudes", contenido);
            Assert.DoesNotContain("cookies.admin", contenido);
            Assert.DoesNotContain("Seed__", contenido);
            Assert.DoesNotContain("MONGO_NO_DISPONIBLE", contenido);
            Assert.DoesNotContain("docs/contratos-api.md", contenido);
        }
    }

    [Fact]
    public async Task Manual_MuestraMetadatosYNoContieneApiKeysReales()
    {
        var rutaMarkdown = ObtenerRutaDocs("manual-integracion.md");
        var markdown = await File.ReadAllTextAsync(rutaMarkdown);
        var versionMarkdown = VersionMarkdownRegex().Match(markdown).Groups[1].Value;
        var fechaMarkdown = FechaMarkdownRegex().Match(markdown).Groups[1].Value;

        using var factory = new PortalWebApplicationFactory();
        using var client = factory.CrearCliente();

        var response = await client.GetAsync("/manual");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(versionMarkdown));
        Assert.False(string.IsNullOrWhiteSpace(fechaMarkdown));
        Assert.Contains($"data-manual-version>{versionMarkdown}</code>", html);
        Assert.Contains($"data-manual-fecha>{fechaMarkdown}</time>", html);

        Assert.Empty(ApiKeyRealRegex().Matches(markdown));
        Assert.Empty(ApiKeyRealRegex().Matches(html));
        Assert.Contains("fak_&lt;prefijo&gt;_&lt;secreto&gt;", html);
    }

    [Fact]
    public async Task Manual_AlModificarArchivoFuente_ReflejaNuevaSeccionSinTocarPlantillas()
    {
        var archivoTemporal = Path.Combine(Path.GetTempPath(), $"manual-it-{Guid.NewGuid():N}.md");
        try
        {
            await File.WriteAllTextAsync(
                archivoTemporal,
                """
                # Manual de integración

                Versión del contrato: `0.2.0` | Última actualización: `2026-11-08`

                ## Sección dinámica de prueba

                Contenido sincronizado desde el archivo configurado.
                """);

            using var factory = new PortalWebApplicationFactory()
                .Configurar("Manual:RutaArchivo", archivoTemporal);
            using var client = factory.CrearCliente();

            var response = await client.GetAsync("/manual");
            var html = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("href=\"#seccion-dinamica-de-prueba\"", html);
            Assert.Contains("<h2 id=\"seccion-dinamica-de-prueba\">Sección dinámica de prueba</h2>", html);
            Assert.Contains("data-manual-version>0.2.0</code>", html);
            Assert.Contains("data-manual-fecha>2026-11-08</time>", html);
        }
        finally
        {
            if (File.Exists(archivoTemporal))
            {
                File.Delete(archivoTemporal);
            }
        }
    }

    [Fact]
    public async Task Manual_ConArchivoInexistente_Responde404()
    {
        using var factory = new PortalWebApplicationFactory()
            .Configurar("Manual:RutaArchivo", "Content/archivo-que-no-existe.md");
        using var client = factory.CrearCliente();

        var response = await client.GetAsync("/manual");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task EnlacesAlManual_PresentesEnHomeLoginYApiKeys()
    {
        using var factory = new PortalWebApplicationFactory();
        using var client = factory.CrearCliente();

        var homeHtml = await (await client.GetAsync("/")).Content.ReadAsStringAsync();
        var loginHtml = await (await client.GetAsync("/login")).Content.ReadAsStringAsync();
        var rutaApiKeysRazor = Path.Combine(
            PortalWebApplicationFactory.ObtenerContentRoot(),
            "Components",
            "Pages",
            "ApiKeys.razor");
        var apiKeysContenido = await File.ReadAllTextAsync(rutaApiKeysRazor);

        Assert.Contains("href=\"/manual\"", homeHtml);
        Assert.DoesNotContain("Guía de integración — Próximamente", homeHtml);
        Assert.Contains("href=\"/manual\"", loginHtml);
        Assert.Contains("href=\"/manual#3-crear-una-api-key\"", apiKeysContenido);
    }

    private static string ObtenerRutaDocs(string nombreArchivo)
    {
        var contentRoot = PortalWebApplicationFactory.ObtenerContentRoot();
        return Path.GetFullPath(Path.Combine(contentRoot, "..", "..", "..", "docs", nombreArchivo));
    }

    private static List<string> ExtraerEncabezadosMarkdown(string[] lineas)
    {
        var encabezados = new List<string>();
        var enBloqueCodigo = false;

        foreach (var linea in lineas)
        {
            if (linea.TrimStart().StartsWith("```", StringComparison.Ordinal))
            {
                enBloqueCodigo = !enBloqueCodigo;
                continue;
            }

            if (enBloqueCodigo)
            {
                continue;
            }

            if (linea.StartsWith("## ", StringComparison.Ordinal) || linea.StartsWith("### ", StringComparison.Ordinal))
            {
                var texto = linea.TrimStart('#').Trim().Replace("`", string.Empty, StringComparison.Ordinal);
                encabezados.Add(texto);
            }
        }

        return encabezados;
    }

    [GeneratedRegex(@"<a class=""manual-toc__link"" href=""#([^""]+)"">([^<]+)</a>", RegexOptions.CultureInvariant)]
    private static partial Regex TocLinkRegex();

    [GeneratedRegex(@"fak_[0-9a-f]{12}_[A-Za-z0-9_-]{43}", RegexOptions.CultureInvariant)]
    private static partial Regex ApiKeyRealRegex();

    [GeneratedRegex(@"Versión del contrato:\s*`([^`]+)`", RegexOptions.CultureInvariant)]
    private static partial Regex VersionMarkdownRegex();

    [GeneratedRegex(@"Última actualización:\s*`([^`]+)`", RegexOptions.CultureInvariant)]
    private static partial Regex FechaMarkdownRegex();
}

