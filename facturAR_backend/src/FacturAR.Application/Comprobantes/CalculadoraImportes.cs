using FacturAR.Domain.Entities;
using FacturAR.Domain.Enums;
using FacturAR.Domain.Exceptions;

namespace FacturAR.Application.Comprobantes;

public sealed record CalculoComprobante(
    IReadOnlyList<ComprobanteItem> Items,
    decimal ImporteNeto,
    decimal ImporteIva,
    decimal ImporteTotal);

public static class CalculadoraImportes
{
    public static CalculoComprobante Calcular(IReadOnlyList<ComprobanteItem> items, TipoComprobante tipo)
    {
        try
        {
            return CalcularInterno(items, tipo);
        }
        catch (OverflowException)
        {
            throw new ComprobanteValidationException("Los importes del comprobante no son válidos.");
        }
    }

    private static CalculoComprobante CalcularInterno(IReadOnlyList<ComprobanteItem> items, TipoComprobante tipo)
    {
        var itemsCalculados = new List<ComprobanteItem>(items.Count);
        var importeNeto = 0m;
        var importeIva = 0m;

        foreach (var item in items)
        {
            var subtotal = Redondear2(item.Cantidad * item.PrecioUnitario);
            importeNeto += subtotal;

            if (tipo == TipoComprobante.FacturaA)
            {
                importeIva += Redondear2(subtotal * item.AlicuotaIva / 100m);
            }

            itemsCalculados.Add(new ComprobanteItem
            {
                Descripcion = item.Descripcion,
                Cantidad = item.Cantidad,
                PrecioUnitario = item.PrecioUnitario,
                AlicuotaIva = item.AlicuotaIva,
                Subtotal = subtotal
            });
        }

        var neto = Redondear2(importeNeto);
        var iva = tipo == TipoComprobante.FacturaA ? Redondear2(importeIva) : 0m;

        return new CalculoComprobante(itemsCalculados, neto, iva, Redondear2(neto + iva));
    }

    private static decimal Redondear2(decimal valor) => Math.Round(valor, 2, MidpointRounding.AwayFromZero);
}
