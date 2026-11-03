using FacturAR.Application.Abstractions;
using FacturAR.Application.Comprobantes;
using FacturAR.Domain.Entities;
using FacturAR.Domain.Enums;
using FacturAR.Infrastructure.Mongo;
using MongoDB.Driver;

namespace FacturAR.Infrastructure.Repositories;

public sealed class ComprobanteRepository : IComprobanteRepository
{
    private static readonly FilterDefinitionBuilder<Comprobante> Filtro = Builders<Comprobante>.Filter;
    private static readonly SortDefinitionBuilder<Comprobante> Orden = Builders<Comprobante>.Sort;
    private const string IndiceIdempotencia = "ux_comprobantes_idempotencia";

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

    public async Task<(IReadOnlyList<Comprobante> Items, long Total)> ListarAsync(
        Guid contribuyenteId,
        FiltroListadoComprobantes filtro,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var condiciones = new List<FilterDefinition<Comprobante>>
        {
            Filtro.Eq(c => c.ContribuyenteId, contribuyenteId)
        };

        if (filtro.Desde is DateTime desde)
        {
            condiciones.Add(Filtro.Gte(c => c.FechaEmision, desde));
        }

        if (filtro.HastaExclusivo is DateTime hastaExclusivo)
        {
            condiciones.Add(Filtro.Lt(c => c.FechaEmision, hastaExclusivo));
        }

        if (filtro.Tipo is TipoComprobante tipo)
        {
            condiciones.Add(Filtro.Eq(c => c.TipoComprobante, tipo));
        }

        if (filtro.Estado is EstadoComprobante estado)
        {
            condiciones.Add(Filtro.Eq(c => c.Estado, estado));
        }

        if (filtro.PuntoVenta is int puntoVenta)
        {
            condiciones.Add(Filtro.Eq(c => c.PuntoVenta, puntoVenta));
        }

        if (!string.IsNullOrWhiteSpace(filtro.ReceptorDocumentoNumero))
        {
            condiciones.Add(Filtro.Eq(c => c.ReceptorDocumentoNumero, filtro.ReceptorDocumentoNumero));
        }

        var consulta = Filtro.And(condiciones);

        var total = await _context.Comprobantes.CountDocumentsAsync(
            consulta,
            cancellationToken: cancellationToken);

        var items = await _context.Comprobantes
            .Find(consulta)
            .Sort(Orden
                .Descending(c => c.FechaEmision)
                .Descending(c => c.Numero)
                .Ascending(c => c.Id))
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task<Comprobante?> GetByIdempotencyKeyAsync(
        Guid contribuyenteId,
        string clave,
        CancellationToken cancellationToken = default)
    {
        var filtro = Filtro.Eq(c => c.ContribuyenteId, contribuyenteId)
            & Filtro.Eq(c => c.IdempotencyKey, clave);

        return await _sesion
            .Find(_context.Comprobantes, filtro)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AddAsync(Comprobante comprobante, CancellationToken cancellationToken = default)
    {
        try
        {
            await _sesion.InsertOneAsync(_context.Comprobantes, comprobante, cancellationToken);
        }
        catch (Exception ex) when (MongoErrores.EsClaveDuplicadaEnIndice(ex, IndiceIdempotencia))
        {
            throw new IdempotenciaDuplicadaException(ex);
        }
    }

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
