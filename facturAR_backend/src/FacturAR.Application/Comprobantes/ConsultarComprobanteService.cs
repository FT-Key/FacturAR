using FacturAR.Application.Abstractions;
using FacturAR.Contracts.Comprobantes;
using FacturAR.Domain.Entities;

namespace FacturAR.Application.Comprobantes;

public sealed class ConsultarComprobanteService(IComprobanteRepository comprobantes)
{
    public Task<Comprobante?> ConsultarEntidadAsync(
        Guid contribuyenteId,
        Guid id,
        CancellationToken ct = default)
        => comprobantes.GetByIdAsync(contribuyenteId, id, ct);

    public async Task<ComprobanteDetalleResponse?> ConsultarAsync(
        Guid contribuyenteId,
        Guid id,
        CancellationToken ct = default)
    {
        var comprobante = await ConsultarEntidadAsync(contribuyenteId, id, ct);

        return comprobante is null ? null : Mapear(comprobante);
    }

    private static ComprobanteDetalleResponse Mapear(Comprobante comprobante)
        => new(
            comprobante.Id,
            (int)comprobante.TipoComprobante,
            comprobante.TipoComprobante.ObtenerDescripcion(),
            comprobante.PuntoVenta,
            comprobante.Numero,
            comprobante.FechaEmision,
            comprobante.Cae,
            comprobante.CaeVencimiento,
            comprobante.EmisorCuit,
            comprobante.EmisorRazonSocial,
            new ReceptorComprobanteResponse(
                comprobante.ReceptorDocumentoTipo,
                comprobante.ReceptorDocumentoNumero,
                comprobante.ReceptorRazonSocial,
                (int)comprobante.ReceptorCondicionIva),
            comprobante.ImporteNeto,
            comprobante.ImporteIva,
            comprobante.ImporteTotal,
            comprobante.Moneda,
            comprobante.Estado.ToString(),
            comprobante.Observaciones,
            comprobante.Items
                .Select(item => new ComprobanteItemResponse(
                    item.Descripcion,
                    item.Cantidad,
                    item.PrecioUnitario,
                    item.AlicuotaIva,
                    item.Subtotal))
                .ToList());
}
