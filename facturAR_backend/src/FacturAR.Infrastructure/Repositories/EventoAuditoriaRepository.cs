using FacturAR.Application.Abstractions;
using FacturAR.Domain.Entities;
using FacturAR.Infrastructure.Mongo;
using MongoDB.Driver;

namespace FacturAR.Infrastructure.Repositories;

public sealed class EventoAuditoriaRepository : IEventoAuditoriaRepository
{
    private const int MaximoHistorial = 100;

    private readonly MongoDbContext _context;
    private readonly MongoSesionActual _sesion;

    public EventoAuditoriaRepository(MongoDbContext context, MongoSesionActual sesion)
    {
        _context = context;
        _sesion = sesion;
    }

    public Task AddAsync(EventoAuditoria evento, CancellationToken ct = default)
        => _sesion.InsertOneAsync(_context.Eventos, evento, ct);

    public async Task<IReadOnlyList<EventoAuditoria>> ListarPorEntidadAsync(
        string entidad,
        string entidadId,
        CancellationToken ct = default)
        => await _sesion
            .Find(
                _context.Eventos,
                Builders<EventoAuditoria>.Filter.Eq(e => e.Entidad, entidad)
                    & Builders<EventoAuditoria>.Filter.Eq(e => e.EntidadId, entidadId))
            .SortBy(e => e.CreatedAt)
            .Limit(MaximoHistorial)
            .ToListAsync(ct);
}
