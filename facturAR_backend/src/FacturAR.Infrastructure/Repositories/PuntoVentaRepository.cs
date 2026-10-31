using FacturAR.Application.Abstractions;
using FacturAR.Domain.Entities;
using FacturAR.Infrastructure.Mongo;
using MongoDB.Driver;

namespace FacturAR.Infrastructure.Repositories;

public sealed class PuntoVentaRepository : IPuntoVentaRepository
{
    private readonly MongoDbContext _context;
    private readonly MongoSesionActual _sesion;

    public PuntoVentaRepository(MongoDbContext context, MongoSesionActual sesion)
    {
        _context = context;
        _sesion = sesion;
    }

    public Task InsertarAsync(PuntoVenta puntoVenta, CancellationToken ct = default)
        => _sesion.InsertOneAsync(_context.PuntosVenta, puntoVenta, ct);

    public async Task<PuntoVenta?> ObtenerPorNumeroAsync(Guid contribuyenteId, int numero, CancellationToken ct = default)
    {
        var filtro = Builders<PuntoVenta>.Filter.Eq(p => p.ContribuyenteId, contribuyenteId)
            & Builders<PuntoVenta>.Filter.Eq(p => p.Numero, numero);

        return await _sesion.Find(_context.PuntosVenta, filtro).FirstOrDefaultAsync(ct);
    }
}
