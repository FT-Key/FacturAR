using System.Globalization;

namespace FacturAR.Web.Services;

public sealed record ComprobanteFiltros(
    DateOnly? Desde = null,
    DateOnly? Hasta = null,
    int? Tipo = null,
    int? Estado = null,
    int? PuntoVenta = null,
    string? Receptor = null,
    int Page = 1,
    int PageSize = 20)
{
    public const int PageSizePorDefecto = 20;
    public const int PageSizeMaximo = 100;

    public static int NormalizarPageSize(int pageSize)
        => pageSize > PageSizeMaximo
            ? PageSizeMaximo
            : pageSize is PageSizePorDefecto or 50 or PageSizeMaximo
                ? pageSize
                : PageSizePorDefecto;

    public ComprobanteFiltros Normalizar()
    {
        var pagina = Page < 1 ? 1 : Page;
        var tamanio = Math.Clamp(NormalizarPageSize(PageSize), 1, PageSizeMaximo);

        return this with { Page = pagina, PageSize = tamanio };
    }

    public string ConstruirQueryString()
    {
        var filtro = Normalizar();
        var partes = new List<string>();

        Agregar(partes, "desde", filtro.Desde?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        Agregar(partes, "hasta", filtro.Hasta?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        Agregar(partes, "tipo", filtro.Tipo?.ToString(CultureInfo.InvariantCulture));
        Agregar(partes, "estado", filtro.Estado?.ToString(CultureInfo.InvariantCulture));
        Agregar(partes, "puntoVenta", filtro.PuntoVenta?.ToString(CultureInfo.InvariantCulture));
        Agregar(partes, "receptor", filtro.Receptor);
        partes.Add($"page={filtro.Page}");
        partes.Add($"pageSize={filtro.PageSize}");

        return string.Join('&', partes);
    }

    private static void Agregar(List<string> partes, string nombre, string? valor)
    {
        if (!string.IsNullOrWhiteSpace(valor))
        {
            partes.Add($"{nombre}={Uri.EscapeDataString(valor.Trim())}");
        }
    }
}
