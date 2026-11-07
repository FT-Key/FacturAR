namespace FacturAR.Web.Services;

public sealed class ManualOptions
{
    public const string SectionName = "Manual";
    public const string DefaultRutaArchivo = "Content/manual-integracion.md";
    public const string DefaultRutaColeccionPostman = "Content/FacturAR.postman_collection.json";
    public const string DefaultVersionContrato = "0.1.0";
    public const string DefaultFechaActualizacion = "2026-11-07";
    public const string DefaultRepositorioManualUrl = "https://github.com/FT-Key/FacturAR/blob/development/docs/manual-integracion.md";
    public const string DefaultRepositorioContratoUrl = "https://github.com/FT-Key/FacturAR/blob/development/docs/contratos-api.md";

    public string RutaArchivo { get; set; } = DefaultRutaArchivo;
    public string RutaColeccionPostman { get; set; } = DefaultRutaColeccionPostman;
    public string VersionContrato { get; set; } = DefaultVersionContrato;
    public string FechaActualizacion { get; set; } = DefaultFechaActualizacion;
    public string RepositorioManualUrl { get; set; } = DefaultRepositorioManualUrl;
    public string RepositorioContratoUrl { get; set; } = DefaultRepositorioContratoUrl;

    public static string? ResolverRutaFisica(
        string? rutaConfigurada,
        string rutaDefecto,
        string rutaDocsRelativa,
        string contentRootPath)
    {
        if (string.IsNullOrWhiteSpace(rutaConfigurada))
        {
            return null;
        }

        if (Path.IsPathRooted(rutaConfigurada))
        {
            return File.Exists(rutaConfigurada) ? rutaConfigurada : null;
        }

        var desdeContentRoot = Path.GetFullPath(Path.Combine(contentRootPath, rutaConfigurada));
        if (File.Exists(desdeContentRoot))
        {
            return desdeContentRoot;
        }

        var desdeBaseDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, rutaConfigurada));
        if (File.Exists(desdeBaseDir))
        {
            return desdeBaseDir;
        }

        if (EsMismaRutaRelativa(rutaConfigurada, rutaDefecto))
        {
            var desdeRepo = Path.GetFullPath(Path.Combine(contentRootPath, "..", "..", "..", rutaDocsRelativa));
            if (File.Exists(desdeRepo))
            {
                return desdeRepo;
            }
        }

        return null;
    }

    private static bool EsMismaRutaRelativa(string rutaA, string rutaB)
    {
        var normalizadaA = rutaA.Trim().Replace('\\', '/');
        var normalizadaB = rutaB.Trim().Replace('\\', '/');
        return string.Equals(normalizadaA, normalizadaB, StringComparison.OrdinalIgnoreCase);
    }
}

