using FacturAR.Application.Abstractions;
using FacturAR.Infrastructure.Mongo;
using FacturAR.Domain.Entities;
using MongoDB.Driver;

namespace FacturAR.Infrastructure.Repositories;

public sealed class ContribuyenteRepository : IContribuyenteRepository
{
    private readonly MongoDbContext _context;

    public ContribuyenteRepository(MongoDbContext context)
    {
        _context = context;
    }

    public async Task<Contribuyente?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Contribuyentes
            .Find(c => c.Id == id)
            .FirstOrDefaultAsync(ct);
    }

    public Task<bool> ExistePorCuitAsync(string cuit, CancellationToken ct = default)
        => _context.Contribuyentes
            .Find(c => c.Cuit == cuit)
            .AnyAsync(ct);
}