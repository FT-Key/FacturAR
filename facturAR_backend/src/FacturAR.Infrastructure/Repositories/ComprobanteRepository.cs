using FacturAR.Application.Abstractions;
using FacturAR.Domain.Entities;
using FacturAR.Infrastructure.Mongo;
using MongoDB.Driver;

namespace FacturAR.Infrastructure.Repositories;

public sealed class ComprobanteRepository : IComprobanteRepository
{
    private readonly MongoDbContext _context;
    private readonly MongoSesionActual _sesion;

    public ComprobanteRepository(MongoDbContext context, MongoSesionActual sesion)
    {
        _context = context;
        _sesion = sesion;
    }

    public async Task<Comprobante?> GetByCaeAsync(string cae, CancellationToken cancellationToken = default)
        => await _context.Comprobantes.Find(c => c.Cae == cae).FirstOrDefaultAsync(cancellationToken);

    public Task AddAsync(Comprobante comprobante, CancellationToken cancellationToken = default)
        => _sesion.InsertOneAsync(_context.Comprobantes, comprobante, cancellationToken);
}
