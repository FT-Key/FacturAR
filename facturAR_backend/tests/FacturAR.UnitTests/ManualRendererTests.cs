using FacturAR.Web.Services;

namespace FacturAR.UnitTests;

public sealed class ManualRendererTests
{
    private readonly ManualRenderer _renderer = new();

    [Fact]
    public void Renderizar_ConHtmlCrudo_EscapaEtiquetasEjecutables()
    {
        const string markdown = """
            ## Seguridad

            Texto con <script>alert(1)</script> e <img src="x" onerror="alert(2)" />.
            """;

        var resultado = _renderer.Renderizar(markdown);

        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", resultado.Html);
        Assert.DoesNotContain("<script>", resultado.Html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<img", resultado.Html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Renderizar_ConEncabezadosH2YH3_GeneraTocEnOrdenConAnclasUnicas()
    {
        const string markdown = """
            # Manual de integración

            ## 1. Solicitar acceso

            ### Requisitos previos

            ## 3. Crear una API key

            ### Requisitos previos
            """;

        var resultado = _renderer.Renderizar(markdown);

        Assert.Equal(4, resultado.TablaContenidos.Count);
        Assert.Equal(new ManualSeccionToc(2, "1-solicitar-acceso", "1. Solicitar acceso"), resultado.TablaContenidos[0]);
        Assert.Equal(new ManualSeccionToc(3, "requisitos-previos", "Requisitos previos"), resultado.TablaContenidos[1]);
        Assert.Equal(new ManualSeccionToc(2, "3-crear-una-api-key", "3. Crear una API key"), resultado.TablaContenidos[2]);
        Assert.Equal(new ManualSeccionToc(3, "requisitos-previos-2", "Requisitos previos"), resultado.TablaContenidos[3]);

        Assert.Contains("<h2 id=\"1-solicitar-acceso\">1. Solicitar acceso</h2>", resultado.Html);
        Assert.Contains("<h3 id=\"requisitos-previos\">Requisitos previos</h3>", resultado.Html);
        Assert.Contains("<h2 id=\"3-crear-una-api-key\">3. Crear una API key</h2>", resultado.Html);
        Assert.Contains("<h3 id=\"requisitos-previos-2\">Requisitos previos</h3>", resultado.Html);
    }

    [Fact]
    public void Renderizar_ConBloquesDeCodigo_AgregaBotonCopiarAccesible()
    {
        const string markdown = """
            ## Ejemplos

            ```bash
            curl -X GET "http://localhost:5171/health"
            ```

            ```javascript
            const res = await fetch("/health");
            ```
            """;

        var resultado = _renderer.Renderizar(markdown);

        Assert.Contains("data-copiar-target=\"manual-codigo-1\"", resultado.Html);
        Assert.Contains("data-copiar-target=\"manual-codigo-2\"", resultado.Html);
        Assert.Contains("aria-label=\"Copiar bloque de código 1 (bash)\"", resultado.Html);
        Assert.Contains("aria-label=\"Copiar bloque de código 2 (javascript)\"", resultado.Html);
        Assert.Contains("aria-live=\"polite\" data-copiar-estado=\"manual-codigo-1\"", resultado.Html);
        Assert.Contains("class=\"language-bash\"", resultado.Html);
        Assert.Contains("class=\"language-javascript\"", resultado.Html);
    }

    [Fact]
    public void Renderizar_ConEnlacesRelativos_LosResuelveContraElRepositorio()
    {
        const string markdown = """
            ## Recursos

            [Contrato](contratos-api.md), [Ancla](#recursos) y [Externo](https://example.com).
            """;

        var resultado = _renderer.Renderizar(
            markdown,
            "https://github.com/FT-Key/FacturAR/blob/development/docs/");

        Assert.Contains(
            "href=\"https://github.com/FT-Key/FacturAR/blob/development/docs/contratos-api.md\"",
            resultado.Html);
        Assert.Contains("href=\"#recursos\"", resultado.Html);
        Assert.Contains("href=\"https://example.com\"", resultado.Html);
    }

    [Fact]
    public void Renderizar_SinRepositorio_MantieneLosEnlacesRelativos()
    {
        const string markdown = "## Recursos\n\n[Contrato](contratos-api.md).";

        var resultado = _renderer.Renderizar(markdown);

        Assert.Contains("href=\"contratos-api.md\"", resultado.Html);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("JavaScript:alert(1)")]
    [InlineData("data:text/html;base64,PHNjcmlwdD5hbGVydCgxKTwvc2NyaXB0Pg==")]
    [InlineData("vbscript:msgbox(1)")]
    public void Renderizar_ConEsquemaPeligroso_NeutralizaElEnlace(string url)
    {
        var markdown = $"## Recursos\n\n[Peligro]({url})";

        var resultado = _renderer.Renderizar(
            markdown,
            "https://github.com/FT-Key/FacturAR/blob/development/docs/");

        Assert.DoesNotContain(url, resultado.Html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("href=\"#\"", resultado.Html);
    }

    [Fact]
    public void Renderizar_ConMetadatosEnMarkdown_ExtraeVersionYFecha()
    {
        const string markdown = """
            # Manual de integración

            Versión del contrato: `0.1.0` | Última actualización: `2026-11-07`

            ## Qué es FacturAR
            """;

        var resultado = _renderer.Renderizar(markdown);

        Assert.Equal("0.1.0", resultado.VersionContratoDetectada);
        Assert.Equal("2026-11-07", resultado.FechaActualizacionDetectada);
    }
}

