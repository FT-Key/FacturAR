using FacturAR.Application.Abstractions;
using FacturAR.Domain.Entities;
using FacturAR.Infrastructure.Mongo;
using MongoDB.Driver;

namespace FacturAR.Infrastructure.Repositories;

public sealed class ComprobanteRepository : IComprobanteRepository
{
    private static readonly FilterDefinitionBuilder<Comprobante> Filtro = Builders<Comprobante>.Filter;

    private readonly MongoDbContext _context;
    private readonly MongoSesionActual _sesion;

    public ComprobanteRepository(MongoDbContext context, MongoSesionActual sesion)
    {
        _context = context;
        _sesion = sesion;
    }

    public async Task<Comprobante?> GetByCaeAsync(string cae, CancellationToken cancellationToken = default)
        => await _context.Comprobantes.Find(c => c.Cae == cae).FirstOrDefaultAsync(cancellationToken);

    public async Task<Comprobante?> GetByIdAsync(
        Guid contribuyenteId,
        Guid id,
        CancellationToken cancellationToken = default)
        => await _context.Comprobantes
            .Find(Filtro.Eq(c => c.Id, id) & Filtro.Eq(c => c.ContribuyenteId, contribuyenteId))
            .FirstOrDefaultAsync(cancellationToken);

    public Task AddAsync(Comprobante comprobante, CancellationToken cancellationToken = default)
        => _sesion.InsertOneAsync(_context.Comprobantes, comprobante, cancellationToken);

    public async Task<bool> AjustarSaldoAsync(
        Guid contribuyenteId,
        Guid originalId,
        decimal? saldoEsperado,
        decimal saldoNuevo,
        CancellationToken cancellationToken = default)
    {
        var resultado = await _sesion.UpdateOneAsync(
            _context.Comprobantes,
            Filtro.Eq(c => c.Id, originalId)
                & Filtro.Eq(c => c.ContribuyenteId, contribuyenteId)
                & Filtro.Eq(c => c.ComprobanteOriginalId, null)
                & Filtro.Eq(c => c.ImporteOriginalAjustado, saldoEsperado),
            Builders<Comprobante>.Update.Set(c => c.ImporteOriginalAjustado, saldoNuevo),
            cancellationToken);

        return resultado.ModifiedCount == 1;
    }
}
