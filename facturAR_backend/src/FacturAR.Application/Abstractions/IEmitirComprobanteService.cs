using FacturAR.Contracts.Comprobantes;

namespace FacturAR.Application.Abstractions;

public interface IEmitirComprobanteService
{
    Task<ComprobanteResponse> EmitirAsync(
        Guid contribuyenteId,
        string? rol,
        EmitirComprobanteRequest request,
        string idempotencyKey,
        CancellationToken ct = default);
}
