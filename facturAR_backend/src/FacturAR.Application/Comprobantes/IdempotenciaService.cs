using FacturAR.Application.Abstractions;

namespace FacturAR.Application.Comprobantes;

public sealed class IdempotenciaService : IIdempotenciaService
{
    private readonly IComprobanteRepository _comprobantes;

    public IdempotenciaService(IComprobanteRepository comprobantes)
    {
        _comprobantes = comprobantes;
    }

    public async Task<ResolucionIdempotencia> ResolverAsync(
        Guid contribuyenteId,
        string clave,
        string payloadHash,
        CancellationToken ct = default)
    {
        var existente = await _comprobantes.GetByIdempotencyKeyAsync(contribuyenteId, clave, ct);

        if (existente is null)
        {
            return new ResolucionIdempotencia(EstadoIdempotencia.Nuevo, null);
        }

        var estado = string.Equals(existente.IdempotencyPayloadHash, payloadHash, StringComparison.Ordinal)
            ? EstadoIdempotencia.Replay
            : EstadoIdempotencia.Conflicto;

        return new ResolucionIdempotencia(estado, existente);
    }
}
