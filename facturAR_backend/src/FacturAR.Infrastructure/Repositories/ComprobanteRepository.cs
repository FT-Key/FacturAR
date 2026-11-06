using FacturAR.Application.Abstractions;
using FacturAR.Application.Comprobantes;
using FacturAR.Domain.Entities;
using FacturAR.Domain.Enums;
using FacturAR.Infrastructure.Mongo;
using MongoDB.Bson;
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

    public async Task<Comprobante?> GetByNumeracionAsync(
        Guid contribuyenteId,
        int puntoVenta,
        TipoComprobante tipo,
        long numero,
        CancellationToken cancellationToken = default)
        => await _context.Comprobantes
            .Find(Filtro.Eq(c => c.ContribuyenteId, contribuyenteId)
                & Filtro.Eq(c => c.PuntoVenta, puntoVenta)
                & Filtro.Eq(c => c.TipoComprobante, tipo)
                & Filtro.Eq(c => c.Numero, numero))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<Comprobante?> GetUltimoEmitidoAsync(
        Guid contribuyenteId,
        int puntoVenta,
        TipoComprobante tipo,
        CancellationToken cancellationToken = default)
        => await _context.Comprobantes
            .Find(Filtro.Eq(c => c.ContribuyenteId, contribuyenteId)
                & Filtro.Eq(c => c.PuntoVenta, puntoVenta)
                & Filtro.Eq(c => c.TipoComprobante, tipo)
                & Filtro.Eq(c => c.Estado, EstadoComprobante.Emitido))
            .Sort(Orden.Descending(c => c.Numero))
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

    public async Task<MetricasDashboard> ObtenerMetricasAsync(
        Guid contribuyenteId,
        DateTime desde,
        DateTime hastaExclusivo,
        CancellationToken cancellationToken = default)
    {
        var filtro = Filtro.Eq(c => c.ContribuyenteId, contribuyenteId)
            & Filtro.Eq(c => c.Estado, EstadoComprobante.Emitido)
            & Filtro.Gte(c => c.FechaEmision, desde)
            & Filtro.Lt(c => c.FechaEmision, hastaExclusivo);

        var grupo = new BsonDocument
        {
            { "_id", 1 },
            { "cantidad", new BsonDocument("$sum", 1) },
            { "importeTotal", new BsonDocument("$sum", "$ImporteTotal") }
        };

        var resultado = await _context.Comprobantes
            .Aggregate()
            .Match(filtro)
            .Group(grupo)
            .FirstOrDefaultAsync(cancellationToken);

        if (resultado is null)
        {
            return new MetricasDashboard(0, 0m);
        }

        return new MetricasDashboard(
            LeerEntero(resultado, "cantidad"),
            LeerDecimal(resultado, "importeTotal"));
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

    private static int LeerEntero(BsonDocument documento, string campo)
        => documento.TryGetValue(campo, out var valor) && valor.IsNumeric
            ? valor.ToInt32()
            : 0;

    private static decimal LeerDecimal(BsonDocument documento, string campo)
    {
        if (!documento.TryGetValue(campo, out var valor))
        {
            return 0m;
        }

        return valor switch
        {
            BsonDecimal128 decimal128 => (decimal)decimal128.Value,
            _ when valor.IsNumeric => Convert.ToDecimal(valor.ToDouble()),
            _ => 0m
        };
    }
}
