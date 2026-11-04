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

    Task ValidarAsync(
        Guid contribuyenteId,
        string? rol,
        EmitirComprobanteRequest request,
        CancellationToken ct = default);
}
