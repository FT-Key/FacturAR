using FacturAR.Domain.Entities;

namespace FacturAR.Application.Abstractions;

public interface IEventoAuditoriaRepository
{
    Task AddAsync(EventoAuditoria evento, CancellationToken ct = default);

    Task<IReadOnlyList<EventoAuditoria>> ListarPorEntidadAsync(
        string entidad,
        string entidadId,
        CancellationToken ct = default);
}
