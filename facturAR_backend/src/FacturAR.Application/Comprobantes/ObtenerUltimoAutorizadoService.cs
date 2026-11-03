using FacturAR.Application.Abstractions;
using FacturAR.Contracts.Comprobantes;
using FacturAR.Domain.Enums;
using FacturAR.Domain.Exceptions;

namespace FacturAR.Application.Comprobantes;

public sealed class ObtenerUltimoAutorizadoService(ISecuenciaRepository secuencias)
{
    public async Task<UltimoAutorizadoResponse> ObtenerAsync(
        Guid contribuyenteId,
        int? tipo,
        int? puntoVenta,
        CancellationToken ct = default)
    {
        if (tipo is not int valorTipo || !Enum.IsDefined(typeof(TipoComprobante), valorTipo))
        {
            throw new ComprobanteValidationException(
                "El tipo de comprobante es obligatorio y debe ser válido.");
        }

        if (puntoVenta is int numeroPuntoVenta && numeroPuntoVenta < 1)
        {
            throw new ComprobanteValidationException("puntoVenta debe ser mayor o igual a 1.");
        }

        var tipoComprobante = (TipoComprobante)valorTipo;

        if (puntoVenta is int punto)
        {
            var ultimo = await secuencias.ObtenerUltimoNumeroAsync(
                contribuyenteId,
                punto,
                tipoComprobante,
                ct);

            return new UltimoAutorizadoResponse(valorTipo, punto, ultimo);
        }

        var maximo = await secuencias.ObtenerMaximoUltimoNumeroAsync(
            contribuyenteId,
            tipoComprobante,
            ct);

        return new UltimoAutorizadoResponse(valorTipo, null, maximo);
    }
}
