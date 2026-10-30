using System.Text.RegularExpressions;
using FacturAR.Application.Abstractions;
using FacturAR.Domain.Entities;
using FacturAR.Domain.Enums;
using FacturAR.Domain.Exceptions;
using FacturAR.Infrastructure.Mongo;
using MongoDB.Bson;
using MongoDB.Driver;

namespace FacturAR.Infrastructure.Repositories;

public sealed class SolicitudAltaRepository : ISolicitudAltaRepository
{
    private static readonly FilterDefinitionBuilder<SolicitudAltaEmpresa> Filtro = Builders<SolicitudAltaEmpresa>.Filter;
    private static readonly UpdateDefinitionBuilder<SolicitudAltaEmpresa> Update = Builders<SolicitudAltaEmpresa>.Update;

    private readonly MongoDbContext _context;
    private readonly MongoSesionActual _sesion;

    public SolicitudAltaRepository(MongoDbContext context, MongoSesionActual sesion)
    {
        _context = context;
        _sesion = sesion;
    }

    public async Task InsertarAsync(SolicitudAltaEmpresa solicitud, CancellationToken ct = default)
    {
        try
        {
            await _sesion.InsertOneAsync(_context.SolicitudesAlta, solicitud, ct);
        }
        catch (MongoException ex) when (MongoErrores.EsClaveDuplicada(ex))
        {
            throw new SolicitudPendienteException();
        }
    }

    public Task<bool> ExistePendientePorCuitAsync(string cuit, CancellationToken ct = default)
        => _sesion
            .Find(_context.SolicitudesAlta, Filtro.Eq(s => s.Cuit, cuit) & Pendiente())
            .AnyAsync(ct);

    public Task<bool> ExistePendientePorEmailAsync(string email, CancellationToken ct = default)
        => _sesion
            .Find(_context.SolicitudesAlta, Filtro.Eq(s => s.EmailSolicitante, email) & Pendiente())
            .AnyAsync(ct);

    public async Task<SolicitudAltaEmpresa?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default)
        => await _sesion
            .Find(_context.SolicitudesAlta, Filtro.Eq(s => s.Id, id))
            .FirstOrDefaultAsync(ct);

    public async Task<(IReadOnlyList<SolicitudAltaEmpresa> Items, long Total)> ListarAsync(
        FiltroSolicitudes filtro,
        CancellationToken ct = default)
    {
        var condiciones = new List<FilterDefinition<SolicitudAltaEmpresa>>();

        if (filtro.Estado is EstadoSolicitud estado)
        {
            condiciones.Add(Filtro.Eq(s => s.Estado, estado));
        }

        if (!string.IsNullOrEmpty(filtro.Cuit))
        {
            condiciones.Add(Filtro.Regex(s => s.Cuit, new BsonRegularExpression($"^{Regex.Escape(filtro.Cuit)}")));
        }

        if (!string.IsNullOrEmpty(filtro.RazonSocial))
        {
            condiciones.Add(Filtro.Regex(s => s.RazonSocial, new BsonRegularExpression(Regex.Escape(filtro.RazonSocial), "i")));
        }

        if (!string.IsNullOrEmpty(filtro.Email))
        {
            condiciones.Add(Filtro.Regex(s => s.EmailSolicitante, new BsonRegularExpression(Regex.Escape(filtro.Email), "i")));
        }

        var filtroFinal = condiciones.Count == 0 ? Filtro.Empty : Filtro.And(condiciones);
        var consulta = _sesion.Find(_context.SolicitudesAlta, filtroFinal);

        var total = await consulta.CountDocumentsAsync(ct);
        var items = await consulta
            .SortByDescending(s => s.CreatedAt)
            .ThenBy(s => s.Id)
            .Skip((filtro.Page - 1) * filtro.PageSize)
            .Limit(filtro.PageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<bool> ActualizarDatosPendienteAsync(SolicitudAltaEmpresa solicitud, CancellationToken ct = default)
    {
        try
        {
            var resultado = await _sesion.UpdateOneAsync(
                _context.SolicitudesAlta,
                Filtro.Eq(s => s.Id, solicitud.Id) & Pendiente(),
                Update
                    .Set(s => s.RazonSocial, solicitud.RazonSocial)
                    .Set(s => s.CondicionIva, solicitud.CondicionIva)
                    .Set(s => s.EmailSolicitante, solicitud.EmailSolicitante)
                    .Set(s => s.Telefono, solicitud.Telefono)
                    .Set(s => s.Domicilio, solicitud.Domicilio)
                    .Set(s => s.UpdatedAt, solicitud.UpdatedAt),
                ct);

            return resultado.MatchedCount == 1;
        }
        catch (MongoException ex) when (MongoErrores.EsClaveDuplicada(ex))
        {
            throw new SolicitudPendienteException();
        }
    }

    public async Task<bool> AprobarPendienteAsync(
        Guid id,
        Guid contribuyenteId,
        Guid invitacionId,
        Guid administradorId,
        DateTime revisadaAt,
        CancellationToken ct = default)
    {
        var resultado = await _sesion.UpdateOneAsync(
            _context.SolicitudesAlta,
            Filtro.Eq(s => s.Id, id) & Pendiente(),
            Update
                .Set(s => s.Estado, EstadoSolicitud.Aprobada)
                .Set(s => s.ContribuyenteId, contribuyenteId)
                .Set(s => s.InvitacionId, invitacionId)
                .Set(s => s.RevisadaPorAdministradorId, administradorId)
                .Set(s => s.RevisadaAt, revisadaAt)
                .Set(s => s.UpdatedAt, revisadaAt),
            ct);

        return resultado.ModifiedCount == 1;
    }

    public async Task<bool> RechazarPendienteAsync(
        Guid id,
        string motivo,
        Guid administradorId,
        DateTime revisadaAt,
        CancellationToken ct = default)
    {
        var resultado = await _sesion.UpdateOneAsync(
            _context.SolicitudesAlta,
            Filtro.Eq(s => s.Id, id) & Pendiente(),
            Update
                .Set(s => s.Estado, EstadoSolicitud.Rechazada)
                .Set(s => s.MotivoRechazo, motivo)
                .Set(s => s.RevisadaPorAdministradorId, administradorId)
                .Set(s => s.RevisadaAt, revisadaAt)
                .Set(s => s.UpdatedAt, revisadaAt),
            ct);

        return resultado.ModifiedCount == 1;
    }

    private static FilterDefinition<SolicitudAltaEmpresa> Pendiente()
        => Filtro.Eq(s => s.Estado, EstadoSolicitud.Pendiente);
}
