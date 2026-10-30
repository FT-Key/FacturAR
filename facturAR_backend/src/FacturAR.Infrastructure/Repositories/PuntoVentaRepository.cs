using FacturAR.Application.Abstractions;
using FacturAR.Domain.Entities;
using FacturAR.Infrastructure.Mongo;

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
}
