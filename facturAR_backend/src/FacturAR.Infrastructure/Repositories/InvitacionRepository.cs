using FacturAR.Application.Abstractions;
using FacturAR.Domain.Entities;
using FacturAR.Domain.Enums;
using FacturAR.Infrastructure.Mongo;
using MongoDB.Driver;

namespace FacturAR.Infrastructure.Repositories;

public sealed class InvitacionRepository : IInvitacionRepository
{
    private static readonly FilterDefinitionBuilder<Invitacion> Filtro = Builders<Invitacion>.Filter;

    private readonly MongoDbContext _context;
    private readonly MongoSesionActual _sesion;

    public InvitacionRepository(MongoDbContext context, MongoSesionActual sesion)
    {
        _context = context;
        _sesion = sesion;
    }

    public Task InsertarAsync(Invitacion invitacion, CancellationToken ct = default)
        => _sesion.InsertOneAsync(_context.Invitaciones, invitacion, ct);

    public async Task<Invitacion?> ObtenerPorTokenHashAsync(string tokenHash, CancellationToken ct = default)
        => await _sesion
            .Find(_context.Invitaciones, Filtro.Eq(i => i.TokenHash, tokenHash))
            .FirstOrDefaultAsync(ct);

    public async Task<Invitacion?> ObtenerVigentePorSolicitudAsync(Guid solicitudId, CancellationToken ct = default)
        => await _sesion
            .Find(
                _context.Invitaciones,
                Filtro.Eq(i => i.SolicitudId, solicitudId) & Filtro.Eq(i => i.Estado, EstadoInvitacion.Pendiente))
            .SortByDescending(i => i.CreatedAt)
            .FirstOrDefaultAsync(ct);

    public async Task<bool> RevocarAsync(Guid id, DateTime revocadaAt, CancellationToken ct = default)
    {
        var resultado = await _sesion.UpdateOneAsync(
            _context.Invitaciones,
            Filtro.Eq(i => i.Id, id) & Filtro.Eq(i => i.Estado, EstadoInvitacion.Pendiente),
            Builders<Invitacion>.Update
                .Set(i => i.Estado, EstadoInvitacion.Revocada)
                .Set(i => i.RevocadaAt, revocadaAt),
            ct);

        return resultado.ModifiedCount == 1;
    }

    public Task RegistrarEnvioAsync(Guid id, DateTime enviadoAt, CancellationToken ct = default)
        => _sesion.UpdateOneAsync(
            _context.Invitaciones,
            Filtro.Eq(i => i.Id, id),
            Builders<Invitacion>.Update
                .Set(i => i.UltimoEnvioAt, enviadoAt)
                .Inc(i => i.IntentosEnvio, 1),
            ct);
}
