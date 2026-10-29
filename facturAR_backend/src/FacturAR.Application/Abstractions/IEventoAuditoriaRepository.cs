using FacturAR.Domain.Entities;

namespace FacturAR.Application.Abstractions;

public interface IEventoAuditoriaRepository
{
    Task AddAsync(EventoAuditoria evento, CancellationToken ct = default);
}