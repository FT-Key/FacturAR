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
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<ManualModel> _logger;
    private readonly IOptions<ManualOptions> _options;
    private readonly ManualRenderer _renderer;

    public ManualModel(
        IOptions<ManualOptions> options,
        ManualRenderer renderer,
        IConfiguration configuration,
        IWebHostEnvironment environment,
        ILogger<ManualModel> logger)
    {
        _options = options;
        _renderer = renderer;
        _configuration = configuration;
        _environment = environment;
        _logger = logger;
    }

    public string HtmlContenido { get; private set; } = string.Empty;
    public IReadOnlyList<ManualSeccionToc> TablaContenidos { get; private set; } = [];
    public string VersionContrato { get; private set; } = ManualOptions.DefaultVersionContrato;
    public string FechaActualizacion { get; private set; } = ManualOptions.DefaultFechaActualizacion;
    public string ApiBaseUrl { get; private set; } = "http://localhost:5171";
    public string OpenApiUrl { get; private set; } = "http://localhost:5171/openapi/v1.json";
    public bool EsDevelopment { get; private set; }
    public string PostmanUrl { get; private set; } = ManualEndpoints.RutaDescargaPostman;
    public string RepositorioManualUrl { get; private set; } = ManualOptions.DefaultRepositorioManualUrl;
    public string RepositorioContratoUrl { get; private set; } = ManualOptions.DefaultRepositorioContratoUrl;

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
                "No se encontró el archivo fuente del manual de integración en '{RutaArchivo}'.",
                opciones.RutaArchivo);
            return NotFound();
        }

        var markdown = await System.IO.File.ReadAllTextAsync(rutaFisica, cancellationToken);
        var documento = _renderer.Renderizar(markdown);

        HtmlContenido = documento.Html;
        TablaContenidos = documento.TablaContenidos;
        VersionContrato = !string.IsNullOrWhiteSpace(opciones.VersionContrato)
            ? opciones.VersionContrato
            : (documento.VersionContratoDetectada ?? ManualOptions.DefaultVersionContrato);
        FechaActualizacion = !string.IsNullOrWhiteSpace(opciones.FechaActualizacion)
            ? opciones.FechaActualizacion
            : (documento.FechaActualizacionDetectada ?? ManualOptions.DefaultFechaActualizacion);

        ApiBaseUrl = (_configuration["Api:BaseUrl"] ?? "http://localhost:5171").TrimEnd('/');
        OpenApiUrl = $"{ApiBaseUrl}/openapi/v1.json";
        EsDevelopment = _environment.IsDevelopment();
        PostmanUrl = ManualEndpoints.RutaDescargaPostman;
        RepositorioManualUrl = opciones.RepositorioManualUrl;
        RepositorioContratoUrl = opciones.RepositorioContratoUrl;

        return Page();
    }
}

