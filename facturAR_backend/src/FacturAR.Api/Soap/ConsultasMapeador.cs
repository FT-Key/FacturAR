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
    private const string NombreServicio = "FacturAR SOAP";
    private const string FormatoFechaVencimiento = "yyyyMMdd";
    private const string FormatoFechaIsoUtc = "yyyy-MM-ddTHH:mm:ssZ";

    public static FECompUltimoAutorizadoResponse AUltimoAutorizadoAprobado(
        UltimoAutorizadoResponse ultimo,
        ComprobanteDetalleResponse? comprobante = null)
    {
        var ultimoNumero = comprobante is not null && comprobante.Numero > ultimo.Numero
            ? comprobante.Numero
            : ultimo.Numero;

        var tieneComprobante = ultimoNumero > 0 && comprobante is not null;

        return new FECompUltimoAutorizadoResponse
        {
            Resultado = ResultadoAprobado,
            PtoVta = ultimo.PuntoVenta ?? comprobante?.PuntoVenta ?? 0,
            TipoComprobante = ultimo.TipoComprobante,
            UltimoNro = ultimoNumero,
            Cae = tieneComprobante ? comprobante!.Cae : null,
            FechaEmision = tieneComprobante ? FormatearFechaUtc(comprobante!.FechaEmision) : null,
            FchVto = tieneComprobante ? FormatearVencimiento(comprobante!.CaeVencimiento) : null
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
            FechaEmision = FormatearFechaUtc(comprobante.FechaEmision),
            Cae = comprobante.Cae,
            FchVto = FormatearVencimiento(comprobante.CaeVencimiento),
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

    public static FECompConsultarResponse AConsultaAprobada(
        ComprobanteResponse comprobante,
        string? cuit = null,
        string? observaciones = null)
        => new()
        {
            Resultado = ResultadoAprobado,
            Cuit = cuit,
            PtoVta = comprobante.PuntoVenta,
            TipoComprobante = comprobante.TipoComprobante,
            Numero = comprobante.Numero,
            FechaEmision = FormatearFechaUtc(comprobante.FechaEmision),
            Cae = comprobante.Cae,
            FchVto = FormatearVencimiento(comprobante.CaeVencimiento),
            DocTipo = comprobante.Receptor.DocumentoTipo,
            DocNro = comprobante.Receptor.DocumentoNumero,
            CondicionIVA = comprobante.Receptor.CondicionIva,
            Moneda = comprobante.Moneda,
            ImporteNeto = comprobante.ImporteNeto,
            ImporteIva = comprobante.ImporteIva,
            ImporteTotal = comprobante.ImporteTotal,
            Estado = comprobante.Estado,
            Observaciones = observaciones
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

    private static string FormatearVencimiento(DateTime vencimiento)
        => vencimiento.ToString(FormatoFechaVencimiento, CultureInfo.InvariantCulture);
}

