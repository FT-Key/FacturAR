using System.Globalization;

namespace FacturAR.Web.Services;

public static class FormatoFacturar
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-AR");

    public static string Importe(decimal valor) => valor.ToString("N2", Cultura);

    public static string FechaLocal(DateTime fecha)
        => DateTime.SpecifyKind(fecha, DateTimeKind.Utc)
            .ToLocalTime()
            .ToString("dd/MM/yyyy HH:mm", Cultura);
}
