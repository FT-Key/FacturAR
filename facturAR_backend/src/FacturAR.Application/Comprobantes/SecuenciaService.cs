using FacturAR.Application.Abstractions;
using FacturAR.Domain.Enums;
using FacturAR.Domain.Exceptions;

namespace FacturAR.Application.Comprobantes;

public sealed class SecuenciaService(
    IPuntoVentaRepository puntoVentaRepository,
    ISecuenciaRepository secuenciaRepository) : ISecuenciaService
{
    public async Task<long> ObtenerSiguienteNumeroAsync(
        Guid contribuyenteId,
        int puntoVenta,
        TipoComprobante tipo,
        CancellationToken ct = default)
    {
        var existente = await puntoVentaRepository.ObtenerPorNumeroAsync(contribuyenteId, puntoVenta, ct);

        if (existente is null)
        {
            throw new PuntoVentaNoEncontradoException();
        }

        if (!existente.Activo)
        {
            throw new PuntoVentaInactivoException();
        }

        return await secuenciaRepository.ObtenerSiguienteNumeroAsync(contribuyenteId, puntoVenta, tipo, ct);
    }
}
