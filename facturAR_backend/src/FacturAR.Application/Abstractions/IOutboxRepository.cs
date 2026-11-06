using FacturAR.Domain.Entities;

namespace FacturAR.Application.Abstractions;

public interface IOutboxRepository
{
    Task<OutboxMessage> EncolarAsync(string tipo, string payload, CancellationToken ct = default);

    Task<IReadOnlyList<OutboxMessage>> ReclamarPendientesAsync(
        int lote,
        IReadOnlyCollection<Guid>? excluir = null,
        CancellationToken ct = default);

    Task MarcarProcesadoAsync(Guid id, CancellationToken ct = default);

    Task MarcarFallidoAsync(Guid id, string error, CancellationToken ct = default);

    Task DevolverAPendienteAsync(Guid id, string? error, CancellationToken ct = default);

    Task<OutboxMessage?> ConsultarAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<OutboxMessage>> ListarPorEstadoAsync(
        string estado,
        DateTime? desde,
        DateTime? hasta,
        int limite,
        CancellationToken ct = default);
}
