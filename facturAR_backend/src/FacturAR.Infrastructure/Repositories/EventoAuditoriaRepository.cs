using FacturAR.Application.Abstractions;
using FacturAR.Infrastructure.Mongo;
using FacturAR.Domain.Entities;
using MongoDB.Driver;

namespace FacturAR.Infrastructure.Repositories;

public sealed class EventoAuditoriaRepository : IEventoAuditoriaRepository
{
    private readonly MongoDbContext _context;

    public EventoAuditoriaRepository(MongoDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(EventoAuditoria evento, CancellationToken ct = default)
    {
        await _context.Eventos.InsertOneAsync(evento, cancellationToken: ct);
    }
}