using FacturAR.Application.Abstractions;
using FacturAR.Domain;
using FacturAR.Domain.Entities;
using FacturAR.Infrastructure.Mongo;
using MongoDB.Driver;

namespace FacturAR.Infrastructure.Repositories;

public sealed class OutboxRepository : IOutboxRepository
{
    private const int MaximoConsulta = 200;

    private readonly MongoDbContext _context;
    private readonly MongoSesionActual _sesion;

    public OutboxRepository(MongoDbContext context, MongoSesionActual sesion)
    {
        _context = context;
        _sesion = sesion;
    }

    public async Task<OutboxMessage> EncolarAsync(string tipo, string payload, CancellationToken ct = default)
    {
        var mensaje = new OutboxMessage
        {
            Tipo = tipo,
            Payload = payload,
            Estado = OutboxEstados.Pendiente,
            Intentos = 0,
            CreatedAt = DateTime.UtcNow
        };

        await _sesion.InsertOneAsync(_context.Outbox, mensaje, ct);
        return mensaje;
    }

    public async Task<IReadOnlyList<OutboxMessage>> ReclamarPendientesAsync(
        int lote,
        IReadOnlyCollection<Guid>? excluir = null,
        CancellationToken ct = default)
    {
        if (lote <= 0)
        {
            return [];
        }

        var filtro = Builders<OutboxMessage>.Filter.Eq(o => o.Estado, OutboxEstados.Pendiente);
        if (excluir is { Count: > 0 })
        {
            filtro &= Builders<OutboxMessage>.Filter.Nin(o => o.Id, excluir);
        }

        var pendientes = await _context.Outbox
            .Find(filtro)
            .SortBy(o => o.CreatedAt)
            .Limit(lote)
            .ToListAsync(ct);

        var reclamados = new List<OutboxMessage>(pendientes.Count);
        foreach (var mensaje in pendientes)
        {
            var reclamado = await _context.Outbox.FindOneAndUpdateAsync(
                Builders<OutboxMessage>.Filter.Eq(o => o.Id, mensaje.Id)
                    & Builders<OutboxMessage>.Filter.Eq(o => o.Estado, OutboxEstados.Pendiente),
                Builders<OutboxMessage>.Update
                    .Set(o => o.Estado, OutboxEstados.Procesando)
                    .Inc(o => o.Intentos, 1),
                new FindOneAndUpdateOptions<OutboxMessage> { ReturnDocument = ReturnDocument.After },
                ct);

            if (reclamado is not null)
            {
                reclamados.Add(reclamado);
            }
        }

        return reclamados;
    }

    public Task MarcarProcesadoAsync(Guid id, CancellationToken ct = default)
        => _context.Outbox.UpdateOneAsync(
            Builders<OutboxMessage>.Filter.Eq(o => o.Id, id)
                & Builders<OutboxMessage>.Filter.Eq(o => o.Estado, OutboxEstados.Procesando),
            Builders<OutboxMessage>.Update
                .Set(o => o.Estado, OutboxEstados.Procesado)
                .Set(o => o.ProcesadoAt, DateTime.UtcNow)
                .Set(o => o.Error, null),
            cancellationToken: ct);

    public Task MarcarFallidoAsync(Guid id, string error, CancellationToken ct = default)
        => _context.Outbox.UpdateOneAsync(
            Builders<OutboxMessage>.Filter.Eq(o => o.Id, id)
                & Builders<OutboxMessage>.Filter.Eq(o => o.Estado, OutboxEstados.Procesando),
            Builders<OutboxMessage>.Update
                .Set(o => o.Estado, OutboxEstados.Fallido)
                .Set(o => o.Error, error),
            cancellationToken: ct);

    public Task DevolverAPendienteAsync(Guid id, string? error, CancellationToken ct = default)
        => _context.Outbox.UpdateOneAsync(
            Builders<OutboxMessage>.Filter.Eq(o => o.Id, id),
            Builders<OutboxMessage>.Update
                .Set(o => o.Estado, OutboxEstados.Pendiente)
                .Set(o => o.Error, error),
            cancellationToken: ct);

    public async Task<OutboxMessage?> ConsultarAsync(Guid id, CancellationToken ct = default)
        => await _context.Outbox
            .Find(Builders<OutboxMessage>.Filter.Eq(o => o.Id, id))
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<OutboxMessage>> ListarPorEstadoAsync(
        string estado,
        DateTime? desde,
        DateTime? hasta,
        int limite,
        CancellationToken ct = default)
    {
        var filtro = Builders<OutboxMessage>.Filter.Eq(o => o.Estado, estado);

        if (desde is DateTime inicio)
        {
            filtro &= Builders<OutboxMessage>.Filter.Gte(o => o.CreatedAt, inicio);
        }

        if (hasta is DateTime fin)
        {
            filtro &= Builders<OutboxMessage>.Filter.Lte(o => o.CreatedAt, fin);
        }

        var tope = Math.Clamp(limite, 1, MaximoConsulta);

        return await _context.Outbox
            .Find(filtro)
            .SortBy(o => o.CreatedAt)
            .Limit(tope)
            .ToListAsync(ct);
    }
}
