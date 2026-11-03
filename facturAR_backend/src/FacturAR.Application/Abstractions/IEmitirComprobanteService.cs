using FacturAR.Application.Comprobantes;
using FacturAR.Contracts.Comprobantes;

namespace FacturAR.Application.Abstractions;

public interface IEmitirComprobanteService
{
    Task<ResultadoEmision> EmitirAsync(
        Guid contribuyenteId,
        string? rol,
        EmitirComprobanteRequest request,
        string idempotencyKey,
        CancellationToken ct = default);
}
