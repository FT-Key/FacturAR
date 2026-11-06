using System.Globalization;
using FacturAR.Contracts.Comprobantes;
using FacturAR.Contracts.Sistema;
using FacturAR.Contracts.Soap;

namespace FacturAR.Api.Soap;

public static class ConsultasMapeador
{
    private const string ResultadoAprobado = "A";
    private const string ResultadoRechazado = "R";
    private const string EstadoDummyOk = "OK";
    private const string NombreServicio = "FacturAR.Api";
    private const string FormatoFechaComprobante = "yyyyMMdd";
    private const string FormatoFechaIsoUtc = "yyyy-MM-ddTHH:mm:ssZ";

    public static FECompUltimoAutorizadoResponse AUltimoAutorizadoAprobado(
        UltimoAutorizadoResponse ultimo,
        ComprobanteDetalleResponse? comprobante = null)
    {
        return new FECompUltimoAutorizadoResponse
        {
            Resultado = ResultadoAprobado,
            PtoVta = ultimo.PuntoVenta ?? comprobante?.PuntoVenta ?? 0,
            TipoComprobante = ultimo.TipoComprobante,
            UltimoNro = comprobante?.Numero ?? 0,
            Cae = comprobante?.Cae,
            FechaEmision = comprobante is null
                ? null
                : FormatearFechaComprobante(comprobante.FechaEmision),
            FchVto = comprobante is null
                ? null
                : FormatearFechaComprobante(comprobante.CaeVencimiento)
        };
    }

    public static FECompUltimoAutorizadoResponse AUltimoAutorizadoRechazado(string codigo, string mensaje)
        => new()
        {
            Resultado = ResultadoRechazado,
            Observaciones = $"{codigo}: {mensaje}"
        };

    public static FECompConsultarResponse AConsultaAprobada(ComprobanteDetalleResponse comprobante)
        => new()
        {
            Resultado = ResultadoAprobado,
            Cuit = comprobante.EmisorCuit,
            PtoVta = comprobante.PuntoVenta,
            TipoComprobante = comprobante.TipoComprobante,
            Numero = comprobante.Numero,
            FechaEmision = FormatearFechaComprobante(comprobante.FechaEmision),
            Cae = comprobante.Cae,
            FchVto = FormatearFechaComprobante(comprobante.CaeVencimiento),
            DocTipo = comprobante.Receptor.DocumentoTipo,
            DocNro = comprobante.Receptor.DocumentoNumero,
            CondicionIVA = comprobante.Receptor.CondicionIva,
            Moneda = comprobante.Moneda,
            ImporteNeto = comprobante.ImporteNeto,
            ImporteIva = comprobante.ImporteIva,
            ImporteTotal = comprobante.ImporteTotal,
            Estado = comprobante.Estado,
            Observaciones = comprobante.Observaciones
        };

    public static FECompConsultarResponse AConsultaRechazada(string codigo, string mensaje)
        => new()
        {
            Resultado = ResultadoRechazado,
            Observaciones = $"{codigo}: {mensaje}"
        };

    public static FEDummyResponse ADummy(BuildInformation build, DateTime? ahoraUtc = null)
        => new()
        {
            Estado = EstadoDummyOk,
            ServerTime = FormatearFechaUtc(ahoraUtc ?? DateTime.UtcNow),
            Version = build.Version,
            Service = NombreServicio
        };

    private static string FormatearFechaUtc(DateTime fecha)
    {
        var utc = fecha.Kind == DateTimeKind.Local
            ? fecha.ToUniversalTime()
            : DateTime.SpecifyKind(fecha, DateTimeKind.Utc);

        return utc.ToString(FormatoFechaIsoUtc, CultureInfo.InvariantCulture);
    }

    private static string FormatearFechaComprobante(DateTime fecha)
        => fecha.ToString(FormatoFechaComprobante, CultureInfo.InvariantCulture);
}
