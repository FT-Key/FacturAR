using FacturAR.Application.Abstractions;
using FacturAR.Domain.Entities;
using FacturAR.Domain.Enums;
using FacturAR.Domain.Exceptions;
using FacturAR.Infrastructure.Mongo;
using MongoDB.Driver;

namespace FacturAR.Infrastructure.Repositories;

public sealed class SolicitudAltaRepository : ISolicitudAltaRepository
{
    private const int CodigoClaveDuplicada = 11000;

    private readonly MongoDbContext _context;

    public SolicitudAltaRepository(MongoDbContext context)
    {
        _context = context;
    }

    public async Task InsertarAsync(SolicitudAltaEmpresa solicitud, CancellationToken ct = default)
    {
        try
        {
            await _context.SolicitudesAlta.InsertOneAsync(solicitud, cancellationToken: ct);
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Code == CodigoClaveDuplicada)
        {
            throw new SolicitudPendienteException();
        }
    }

    public Task<bool> ExistePendientePorCuitAsync(string cuit, CancellationToken ct = default)
        => _context.SolicitudesAlta
            .Find(s => s.Cuit == cuit && s.Estado == EstadoSolicitud.Pendiente)
            .AnyAsync(ct);

    public Task<bool> ExistePendientePorEmailAsync(string email, CancellationToken ct = default)
        => _context.SolicitudesAlta
            .Find(s => s.EmailSolicitante == email && s.Estado == EstadoSolicitud.Pendiente)
            .AnyAsync(ct);
}
