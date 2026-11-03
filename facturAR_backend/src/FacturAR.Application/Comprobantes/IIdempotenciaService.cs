using FacturAR.Domain.Entities;

namespace FacturAR.Application.Comprobantes;

public enum EstadoIdempotencia
{
    Nuevo,
    Replay,
    Conflicto
}

public sealed record ResolucionIdempotencia(EstadoIdempotencia Estado, Comprobante? Comprobante);

public interface IIdempotenciaService
{
    Task<ResolucionIdempotencia> ResolverAsync(
        Guid contribuyenteId,
        string clave,
        string payloadHash,
        CancellationToken ct = default);
}
