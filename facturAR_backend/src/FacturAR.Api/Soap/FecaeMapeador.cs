using System.Globalization;
using FacturAR.Contracts.Comprobantes;
using FacturAR.Contracts.Soap;

namespace FacturAR.Api.Soap;

public static class FecaeMapeador
{
    private const int DocumentoTipoConsumidorFinal = 99;
    private const int DocumentoTipoCuit = 80;
    private const string DocumentoTipoCuitTexto = "CUIT";
    private const string ResultadoAprobado = "A";
    private const string ResultadoRechazado = "R";
    private const string FormatoFechaVencimiento = "yyyyMMdd";

    public static EmitirComprobanteRequest AEmision(FECAESolicitarRequest request)
        => AEmision(
            request.PtoVta,
            request.TipoComprobante,
            request.DocTipo,
            request.DocNro,
            request.CondicionIVA,
            request.Items);

    public static EmitirComprobanteRequest AEmision(FECAEGenerarRequest request)
        => AEmision(
            request.PtoVta,
            request.TipoComprobante,
            request.DocTipo,
            request.DocNro,
            request.CondicionIVA,
            request.Items);

    public static FECAESolicitarResponse AAprobada(ComprobanteResponse comprobante)
        => new()
        {
            FeCabResp = new FeCabResp
            {
                Resultado = ResultadoAprobado,
                CodAutorizacion = comprobante.Cae,
                FchVto = comprobante.CaeVencimiento.ToString(FormatoFechaVencimiento, CultureInfo.InvariantCulture)
            }
        };

    public static FECAESolicitarResponse ARechazada(string codigo, string mensaje)
        => new()
        {
            FeCabResp = new FeCabResp
            {
                Resultado = ResultadoRechazado,
                Observaciones = $"{codigo}: {mensaje}"
            }
        };

    public static FECAEGenerarResponse GenerarAprobada()
        => new()
        {
            Respuesta = new Respuesta { Resultado = ResultadoAprobado }
        };

    public static FECAEGenerarResponse GenerarRechazada(string codigo, string mensaje)
        => new()
        {
            Respuesta = new Respuesta
            {
                Resultado = ResultadoRechazado,
                Error = codigo,
                Observaciones = mensaje
            }
        };

    private static EmitirComprobanteRequest AEmision(
        int puntoVenta,
        int tipoComprobante,
        int documentoTipo,
        long documentoNumero,
        int condicionIva,
        List<FecaeItem>? items)
        => new(
            tipoComprobante,
            puntoVenta,
            null,
            new ReceptorComprobanteRequest(
                MapearDocumentoTipo(documentoTipo),
                documentoNumero.ToString(CultureInfo.InvariantCulture),
                null,
                condicionIva),
            items?
                .Select(item => new ItemComprobanteRequest(
                    item.Descripcion,
                    item.Cantidad,
                    item.PrecioUnitario,
                    item.AlicuotaIVA))
                .ToList(),
            null,
            null);

    private static string MapearDocumentoTipo(int documentoTipo) => documentoTipo switch
    {
        DocumentoTipoConsumidorFinal => DocumentoTipoConsumidorFinal.ToString(CultureInfo.InvariantCulture),
        DocumentoTipoCuit => DocumentoTipoCuitTexto,
        _ => documentoTipo.ToString(CultureInfo.InvariantCulture)
    };
}
