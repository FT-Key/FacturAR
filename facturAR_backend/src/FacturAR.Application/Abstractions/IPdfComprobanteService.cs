using FacturAR.Domain.Entities;

namespace FacturAR.Application.Abstractions;

public interface IPdfComprobanteService
{
    Task<byte[]> GenerarAsync(Comprobante comprobante, CancellationToken ct = default);
}
