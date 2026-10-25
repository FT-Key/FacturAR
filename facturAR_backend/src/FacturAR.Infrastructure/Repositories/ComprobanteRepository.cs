using FacturAR.Application.Abstractions;
using FacturAR.Domain.Entities;
using FacturAR.Infrastructure.Mongo;
using MongoDB.Driver;

namespace FacturAR.Infrastructure.Repositories;

public sealed class ComprobanteRepository(MongoDbContext context) : IComprobanteRepository
{
    public async Task<Comprobante?> GetByCaeAsync(string cae, CancellationToken cancellationToken = default)
        => await context.Comprobantes.Find(c => c.Cae == cae).FirstOrDefaultAsync(cancellationToken);

    public Task AddAsync(Comprobante comprobante, CancellationToken cancellationToken = default)
        => context.Comprobantes.InsertOneAsync(comprobante, cancellationToken: cancellationToken);
}
