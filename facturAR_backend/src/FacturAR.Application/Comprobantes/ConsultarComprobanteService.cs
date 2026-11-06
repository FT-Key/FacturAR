using FacturAR.Application.Abstractions;
using FacturAR.Contracts.Comprobantes;
using FacturAR.Domain.Entities;
using FacturAR.Domain.Enums;
using FacturAR.Domain.Exceptions;

namespace FacturAR.Application.Comprobantes;

public sealed class ConsultarComprobanteService(IComprobanteRepository comprobantes)
{
    private const int PuntoVentaMinimo = 1;
    private const int PuntoVentaMaximo = 9999;

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

    public async Task<ComprobanteDetalleResponse?> ConsultarPorNumeroAsync(
        Guid contribuyenteId,
        int tipoComprobante,
        int puntoVenta,
        long numero,
        CancellationToken ct = default)
    {
        ValidarTipoYPuntoVenta(tipoComprobante, puntoVenta);

        if (numero < 1)
        {
            throw new ComprobanteValidationException("El número de comprobante debe ser mayor o igual a 1.");
        }

        var comprobante = await comprobantes.GetByNumeracionAsync(
            contribuyenteId,
            puntoVenta,
            (TipoComprobante)tipoComprobante,
            numero,
            ct);

        return comprobante is null ? null : Mapear(comprobante);
    }

    public async Task<ComprobanteDetalleResponse?> ConsultarUltimoEmitidoAsync(
        Guid contribuyenteId,
        int tipoComprobante,
        int puntoVenta,
        CancellationToken ct = default)
    {
        ValidarTipoYPuntoVenta(tipoComprobante, puntoVenta);

        var comprobante = await comprobantes.GetUltimoEmitidoAsync(
            contribuyenteId,
            puntoVenta,
            (TipoComprobante)tipoComprobante,
            ct);

        return comprobante is null ? null : Mapear(comprobante);
    }

    private static void ValidarTipoYPuntoVenta(int tipoComprobante, int puntoVenta)
    {
        if (!Enum.IsDefined(typeof(TipoComprobante), tipoComprobante))
        {
            throw new ComprobanteValidationException("El tipo de comprobante no es válido.");
        }

        if (puntoVenta is < PuntoVentaMinimo or > PuntoVentaMaximo)
        {
            throw new ComprobanteValidationException("El punto de venta debe estar entre 1 y 9999.");
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
