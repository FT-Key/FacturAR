using FacturAR.Application.Abstractions;
using FacturAR.Contracts.Comprobantes;
using FacturAR.Domain.Entities;
using FacturAR.Domain.Enums;
using FacturAR.Domain.Exceptions;

namespace FacturAR.Application.Comprobantes;

public sealed class ConsultarComprobanteService(IComprobanteRepository comprobantes)
{
    public async Task<ComprobanteDetalleResponse?> ConsultarAsync(
        Guid contribuyenteId,
        Guid id,
        CancellationToken ct = default)
    {
        var comprobante = await comprobantes.GetByIdAsync(contribuyenteId, id, ct);

        return comprobante is null ? null : Mapear(comprobante);
    }

    public async Task<ComprobanteDetalleResponse?> ConsultarPorNumeroAsync(
        Guid contribuyenteId,
        int tipo,
        int puntoVenta,
        long numero,
        CancellationToken ct = default)
    {
        ValidarTipoYPuntoVenta(tipo, puntoVenta);

        if (numero < 1)
        {
            throw new ComprobanteValidationException("El número de comprobante debe ser mayor o igual a 1.");
        }

        var comprobante = await comprobantes.GetByNumeracionAsync(
            contribuyenteId,
            puntoVenta,
            (TipoComprobante)tipo,
            numero,
            ct);

        return comprobante is null ? null : Mapear(comprobante);
    }

    public async Task<ComprobanteDetalleResponse?> ConsultarUltimoEmitidoAsync(
        Guid contribuyenteId,
        int tipo,
        int puntoVenta,
        CancellationToken ct = default)
    {
        ValidarTipoYPuntoVenta(tipo, puntoVenta);

        var comprobante = await comprobantes.GetUltimoEmitidoAsync(
            contribuyenteId,
            puntoVenta,
            (TipoComprobante)tipo,
            ct);

        return comprobante is null ? null : Mapear(comprobante);
    }

    private static void ValidarTipoYPuntoVenta(int tipo, int puntoVenta)
    {
        if (!Enum.IsDefined(typeof(TipoComprobante), tipo))
        {
            throw new ComprobanteValidationException("El tipo de comprobante indicado no es válido.");
        }

        if (puntoVenta < 1)
        {
            throw new ComprobanteValidationException("puntoVenta debe ser mayor o igual a 1.");
        }
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
