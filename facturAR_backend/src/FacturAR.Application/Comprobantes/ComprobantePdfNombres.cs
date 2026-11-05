using FacturAR.Domain.Entities;

namespace FacturAR.Application.Comprobantes;

public static class ComprobantePdfNombres
{
    public static string NombreArchivo(Comprobante comprobante)
        => $"{comprobante.PuntoVenta:D4}-{comprobante.Numero:D8}.pdf";

    public static string UrlVerificacion(string portalBaseUrl, string cae)
        => $"{NormalizarBaseUrl(portalBaseUrl)}/verificar?cae={Uri.EscapeDataString(cae)}";

    public static string NormalizarBaseUrl(string portalBaseUrl)
        => portalBaseUrl.Trim().TrimEnd('/');
}
