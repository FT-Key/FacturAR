using FacturAR.Application.Abstractions;
using FacturAR.Domain.Entities;
using FacturAR.Infrastructure.Mongo;
using MongoDB.Driver;

namespace FacturAR.Infrastructure.Repositories;

public sealed class ParametrosRepository : IParametrosRepository
{
    private readonly MongoDbContext _context;

    public ParametrosRepository(MongoDbContext context)
    {
        _context = context;
    }

    public async Task<string?> ObtenerValorAsync(
        Guid contribuyenteId,
        string clave,
        CancellationToken ct = default)
    {
        var filtro = Builders<Parametro>.Filter.Eq(p => p.ContribuyenteId, (Guid?)contribuyenteId)
            & Builders<Parametro>.Filter.Eq(p => p.Clave, clave);

        return await _context.Parametros
            .Find(filtro)
            .Project(p => p.Valor)
            .FirstOrDefaultAsync(ct);
    }
}
