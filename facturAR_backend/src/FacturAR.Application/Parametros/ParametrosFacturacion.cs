using System.Globalization;

namespace FacturAR.Application.Parametros;

public sealed class ParametrosFacturacion
{
    public const string SectionName = "Facturacion";

    public const int DiasVencimientoCaePredeterminado = 10;
    public const int PuntoVentaPredeterminado = 1;
    public const string MonedaPredeterminada = "ARS";
    public const int PaginaPredeterminada = 20;
    public const int PaginaMaximaPredeterminada = 100;

    public static IReadOnlyList<decimal> AlicuotasPredeterminadas { get; } =
        Array.AsReadOnly<decimal>([0m, 5m, 10.5m, 21m, 27m]);

    public static IReadOnlyList<string> Claves { get; } =
        Array.AsReadOnly<string>(
        [
            nameof(DiasVencimientoCae),
            nameof(AlicuotasPermitidas),
            nameof(PuntoVentaPorDefecto),
            nameof(Moneda),
            nameof(PaginaPorDefecto),
            nameof(PaginaMaxima)
        ]);

    public int DiasVencimientoCae { get; set; } = DiasVencimientoCaePredeterminado;

    public decimal[] AlicuotasPermitidas { get; set; } = [.. AlicuotasPredeterminadas];

    public int PuntoVentaPorDefecto { get; set; } = PuntoVentaPredeterminado;

    public string Moneda { get; set; } = MonedaPredeterminada;

    public int PaginaPorDefecto { get; set; } = PaginaPredeterminada;

    public int PaginaMaxima { get; set; } = PaginaMaximaPredeterminada;

    public IReadOnlyDictionary<string, string> ObtenerValores()
    {
        var diasVencimiento = DiasVencimientoCae > 0 ? DiasVencimientoCae : DiasVencimientoCaePredeterminado;
        var puntoVenta = PuntoVentaPorDefecto > 0 ? PuntoVentaPorDefecto : PuntoVentaPredeterminado;
        var moneda = string.IsNullOrWhiteSpace(Moneda) ? MonedaPredeterminada : Moneda.Trim();
        var paginaPorDefecto = PaginaPorDefecto > 0 ? PaginaPorDefecto : PaginaPredeterminada;
        var paginaMaxima = PaginaMaxima > 0 ? PaginaMaxima : PaginaMaximaPredeterminada;
        var alicuotas = AlicuotasPermitidas.Length > 0 ? AlicuotasPermitidas : AlicuotasPredeterminadas;

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [nameof(DiasVencimientoCae)] = diasVencimiento.ToString(CultureInfo.InvariantCulture),
            [nameof(AlicuotasPermitidas)] = string.Join(
                ';',
                alicuotas.Select(a => a.ToString(CultureInfo.InvariantCulture))),
            [nameof(PuntoVentaPorDefecto)] = puntoVenta.ToString(CultureInfo.InvariantCulture),
            [nameof(Moneda)] = moneda,
            [nameof(PaginaPorDefecto)] = paginaPorDefecto.ToString(CultureInfo.InvariantCulture),
            [nameof(PaginaMaxima)] = paginaMaxima.ToString(CultureInfo.InvariantCulture)
        };
    }

    public string? ObtenerValor(string clave)
        => ObtenerValores().TryGetValue(clave.Trim(), out var valor) ? valor : null;
}
