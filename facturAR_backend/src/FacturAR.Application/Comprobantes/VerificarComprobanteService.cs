using FacturAR.Application.Abstractions;
using FacturAR.Contracts.Comprobantes;
using FacturAR.Domain.Validation;

namespace FacturAR.Application.Comprobantes;

public sealed class VerificarComprobanteService(IComprobanteRepository repository)
{
    public async Task<VerificacionResponse?> VerificarAsync(string cae, CancellationToken cancellationToken = default)
    {
        if (!CaeValidator.IsValid(cae))
        {
            return null;
        }

        var comprobante = await repository.GetByCaeAsync(cae, cancellationToken);
        if (comprobante is null)
        {
            return null;
        }

        return new VerificacionResponse(
            comprobante.Cae,
            comprobante.EmisorCuit,
            comprobante.EmisorRazonSocial,
            (int)comprobante.TipoComprobante,
            comprobante.TipoComprobante.ObtenerDescripcion(),
            comprobante.PuntoVenta,
            comprobante.Numero,
            comprobante.FechaEmision,
            comprobante.ImporteTotal,
            comprobante.Moneda,
            comprobante.Estado.ToString(),
            comprobante.CaeVencimiento);
    }
}
