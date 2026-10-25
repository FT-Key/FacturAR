using FacturAR.Domain.Enums;

namespace FacturAR.Application.Comprobantes;

public static class TipoComprobanteExtensions
{
    public static string ObtenerDescripcion(this TipoComprobante tipo) => tipo switch
    {
        TipoComprobante.FacturaA => "Factura A",
        TipoComprobante.NotaDebitoA => "Nota de Débito A",
        TipoComprobante.NotaCreditoA => "Nota de Crédito A",
        TipoComprobante.FacturaB => "Factura B",
        TipoComprobante.NotaDebitoB => "Nota de Débito B",
        TipoComprobante.NotaCreditoB => "Nota de Crédito B",
        TipoComprobante.FacturaC => "Factura C",
        TipoComprobante.NotaDebitoC => "Nota de Débito C",
        TipoComprobante.NotaCreditoC => "Nota de Crédito C",
        _ => "Comprobante"
    };
}
