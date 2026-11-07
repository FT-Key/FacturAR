using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Markdig;

namespace FacturAR.Web.Services;

public sealed record ManualSeccionToc(int Nivel, string Id, string Titulo);

public sealed record ManualDocumentoRenderizado(
    string Html,
    IReadOnlyList<ManualSeccionToc> TablaContenidos,
    string? VersionContratoDetectada,
    string? FechaActualizacionDetectada);

public sealed partial class ManualRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UsePipeTables()
        .DisableHtml()
        .Build();

    public ManualDocumentoRenderizado Renderizar(string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return new ManualDocumentoRenderizado(string.Empty, [], null, null);
        }

        var versionDetectada = ExtraerCoincidencia(VersionContratoRegex(), markdown);
        var fechaDetectada = ExtraerCoincidencia(FechaActualizacionRegex(), markdown);

        var htmlBase = Markdown.ToHtml(markdown, Pipeline);
        var secciones = new List<ManualSeccionToc>();
        var idsUsados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var htmlConAnclas = HeadingRegex().Replace(htmlBase, match =>
        {
            var nivel = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            var contenidoHtml = match.Groups[2].Value;
            var textoPlano = ExtraerTextoPlano(contenidoHtml);
            var id = GenerarSlugUnico(textoPlano, idsUsados);

            if (nivel is 2 or 3)
            {
                secciones.Add(new ManualSeccionToc(nivel, id, textoPlano));
            }

            return $"<h{nivel} id=\"{id}\">{contenidoHtml}</h{nivel}>";
        });

        var indiceCodigo = 0;
        var htmlFinal = CodeBlockRegex().Replace(htmlConAnclas, match =>
        {
            indiceCodigo++;
            var lenguajeCrudo = match.Groups[1].Value;
            var contenidoCodigo = match.Groups[2].Value;
            var tieneLenguaje = !string.IsNullOrWhiteSpace(lenguajeCrudo);
            var etiquetaLenguaje = tieneLenguaje
                ? WebUtility.HtmlEncode(lenguajeCrudo.Trim())
                : "texto";
            var atributoClase = tieneLenguaje
                ? $" class=\"language-{etiquetaLenguaje}\""
                : string.Empty;
            var codigoId = $"manual-codigo-{indiceCodigo}";

            return string.Concat(
                "<div class=\"code-block\">",
                "<div class=\"code-block__header\">",
                $"<span class=\"code-block__lang\">{etiquetaLenguaje}</span>",
                $"<button type=\"button\" class=\"code-copy\" data-copiar data-copiar-manual data-copiar-target=\"{codigoId}\" aria-label=\"Copiar bloque de código {indiceCodigo} ({etiquetaLenguaje})\">Copiar</button>",
                "</div>",
                $"<pre id=\"{codigoId}\"><code{atributoClase}>{contenidoCodigo}</code></pre>",
                $"<span class=\"sr-only\" aria-live=\"polite\" data-copiar-estado=\"{codigoId}\"></span>",
                "</div>");
        });

        return new ManualDocumentoRenderizado(htmlFinal, secciones, versionDetectada, fechaDetectada);
    }

    public static string GenerarSlug(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return "seccion";
        }

        var normalizado = texto.Normalize(NormalizationForm.FormD);
        var constructor = new StringBuilder(normalizado.Length);
        var guionPendiente = false;

        foreach (var caracter in normalizado)
        {
            var categoria = CharUnicodeInfo.GetUnicodeCategory(caracter);
            if (categoria == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            var minuscula = char.ToLowerInvariant(caracter);
            if ((minuscula >= 'a' && minuscula <= 'z') || (minuscula >= '0' && minuscula <= '9'))
            {
                if (guionPendiente && constructor.Length > 0)
                {
                    constructor.Append('-');
                }

                constructor.Append(minuscula);
                guionPendiente = false;
            }
            else if (constructor.Length > 0)
            {
                guionPendiente = true;
            }
        }

        return constructor.Length > 0 ? constructor.ToString() : "seccion";
    }

    private static string GenerarSlugUnico(string texto, HashSet<string> usados)
    {
        var baseSlug = GenerarSlug(texto);
        if (usados.Add(baseSlug))
        {
            return baseSlug;
        }

        var sufijo = 2;
        while (!usados.Add($"{baseSlug}-{sufijo}"))
        {
            sufijo++;
        }

        return $"{baseSlug}-{sufijo}";
    }

    private static string ExtraerTextoPlano(string html)
    {
        var sinTags = HtmlTagRegex().Replace(html, string.Empty);
        return WebUtility.HtmlDecode(sinTags).Trim();
    }

    private static string? ExtraerCoincidencia(Regex regex, string texto)
    {
        var match = regex.Match(texto);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    [GeneratedRegex(@"<h([1-6])>(.*?)</h\1>", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex HeadingRegex();

    [GeneratedRegex(@"<pre><code(?:\s+class=""language-([^""]+)"")?>(.*?)</code></pre>", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex CodeBlockRegex();

    [GeneratedRegex(@"<[^>]+>", RegexOptions.CultureInvariant)]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex(@"Versi[oó]n del contrato:\s*`?([0-9]+\.[0-9]+\.[0-9]+)`?", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex VersionContratoRegex();

    [GeneratedRegex(@"[ÚU]ltima actualizaci[oó]n:\s*`?([0-9]{4}-[0-9]{2}-[0-9]{2})`?", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FechaActualizacionRegex();
}

