using FacturAR.Web.Endpoints;
using FacturAR.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace FacturAR.Web.Pages;

[AllowAnonymous]
public sealed class ManualModel : PageModel
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<ManualModel> _logger;
    private readonly IOptions<ManualOptions> _options;
    private readonly ManualRenderer _renderer;

    public ManualModel(
        IOptions<ManualOptions> options,
        ManualRenderer renderer,
        IWebHostEnvironment environment,
        ILogger<ManualModel> logger)
    {
        _options = options;
        _renderer = renderer;
        _environment = environment;
        _logger = logger;
    }

    public string HtmlContenido { get; private set; } = string.Empty;
    public IReadOnlyList<ManualSeccionToc> TablaContenidos { get; private set; } = [];
    public string VersionContrato { get; private set; } = ManualOptions.DefaultVersionContrato;
    public string FechaActualizacion { get; private set; } = ManualOptions.DefaultFechaActualizacion;
    public string PostmanUrl { get; private set; } = ManualEndpoints.RutaDescargaPostman;

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var opciones = _options.Value;
        var rutaFisica = ManualOptions.ResolverRutaFisica(
            opciones.RutaArchivo,
            ManualOptions.DefaultRutaArchivo,
            Path.Combine("docs", "manual-integracion.md"),
            _environment.ContentRootPath);

        if (rutaFisica is null)
        {
            _logger.LogError(
                "No se encontró el archivo fuente del manual de integración en '{RutaArchivo}' (ruta por defecto '{RutaDefecto}') bajo '{ContentRoot}'.",
                opciones.RutaArchivo,
                ManualOptions.DefaultRutaArchivo,
                _environment.ContentRootPath);
            return NotFound();
        }

        var markdown = await System.IO.File.ReadAllTextAsync(rutaFisica, cancellationToken);
        var documento = _renderer.Renderizar(markdown);

        HtmlContenido = documento.Html;
        TablaContenidos = documento.TablaContenidos;
        VersionContrato = documento.VersionContratoDetectada
            ?? (!string.IsNullOrWhiteSpace(opciones.VersionContrato)
                ? opciones.VersionContrato
                : ManualOptions.DefaultVersionContrato);
        FechaActualizacion = documento.FechaActualizacionDetectada
            ?? (!string.IsNullOrWhiteSpace(opciones.FechaActualizacion)
                ? opciones.FechaActualizacion
                : ManualOptions.DefaultFechaActualizacion);

        PostmanUrl = ManualEndpoints.RutaDescargaPostman;

        return Page();
    }
}

