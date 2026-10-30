using FacturAR.Application.Abstractions;
using FacturAR.Domain.Entities;
using FacturAR.Domain.Exceptions;
using FacturAR.Infrastructure.Mongo;
using MongoDB.Driver;

namespace FacturAR.Infrastructure.Repositories;

public sealed class ContribuyenteRepository : IContribuyenteRepository
{
    private readonly MongoDbContext _context;
    private readonly MongoSesionActual _sesion;

    public ContribuyenteRepository(MongoDbContext context, MongoSesionActual sesion)
    {
        _context = context;
        _sesion = sesion;
    }

    public async Task<Contribuyente?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _sesion
            .Find(_context.Contribuyentes, Builders<Contribuyente>.Filter.Eq(c => c.Id, id))
            .FirstOrDefaultAsync(ct);
    }

    public Task<bool> ExistePorCuitAsync(string cuit, CancellationToken ct = default)
        => _sesion
            .Find(_context.Contribuyentes, Builders<Contribuyente>.Filter.Eq(c => c.Cuit, cuit))
            .AnyAsync(ct);

    public async Task InsertarAsync(Contribuyente contribuyente, CancellationToken ct = default)
    {
        try
        {
            await _sesion.InsertOneAsync(_context.Contribuyentes, contribuyente, ct);
        }
        catch (MongoException ex) when (MongoErrores.EsClaveDuplicada(ex))
        {
            throw new CuitYaRegistradoException();
        }
    }
}
