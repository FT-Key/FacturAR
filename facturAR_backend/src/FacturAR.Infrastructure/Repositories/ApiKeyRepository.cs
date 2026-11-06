using FacturAR.Application.Abstractions;
using FacturAR.Domain.Entities;
using FacturAR.Domain.Exceptions;
using FacturAR.Infrastructure.Mongo;
using MongoDB.Driver;

namespace FacturAR.Infrastructure.Repositories;

public sealed class ApiKeyRepository : IApiKeyRepository
{
    private static readonly FilterDefinitionBuilder<ApiKey> Filtro = Builders<ApiKey>.Filter;
    private static readonly UpdateDefinitionBuilder<ApiKey> Update = Builders<ApiKey>.Update;

    private readonly MongoDbContext _context;
    private readonly MongoSesionActual _sesion;

    public ApiKeyRepository(MongoDbContext context, MongoSesionActual sesion)
    {
        _context = context;
        _sesion = sesion;
    }

    public async Task<ApiKey?> GetByIdAsync(Guid id, Guid contribuyenteId, CancellationToken ct = default)
        => await _sesion
            .Find(_context.ApiKeys, Filtro.Eq(a => a.Id, id) & Filtro.Eq(a => a.ContribuyenteId, contribuyenteId))
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<ApiKey>> ListByContribuyenteAsync(
        Guid contribuyenteId,
        CancellationToken ct = default)
        => await _sesion
            .Find(_context.ApiKeys, Filtro.Eq(a => a.ContribuyenteId, contribuyenteId))
            .SortByDescending(a => a.CreatedAt)
            .ThenBy(a => a.Id)
            .ToListAsync(ct);

    public async Task<ApiKey?> GetByPrefixAsync(string prefix, CancellationToken ct = default)
        => await _sesion
            .Find(_context.ApiKeys, Filtro.Eq(a => a.Prefix, prefix))
            .FirstOrDefaultAsync(ct);

    public async Task InsertAsync(ApiKey apiKey, CancellationToken ct = default)
    {
        try
        {
            await _sesion.InsertOneAsync(_context.ApiKeys, apiKey, ct);
        }
        catch (MongoException ex) when (MongoErrores.EsClaveDuplicada(ex))
        {
            throw new ApiKeyPrefijoDuplicadoException();
        }
    }

    public async Task UpdateAsync(ApiKey apiKey, CancellationToken ct = default)
    {
        UpdateResult resultado;

        try
        {
            resultado = await _sesion.UpdateOneAsync(
                _context.ApiKeys,
                Filtro.Eq(a => a.Id, apiKey.Id) & Filtro.Eq(a => a.ContribuyenteId, apiKey.ContribuyenteId),
                Update
                    .Set(a => a.Nombre, apiKey.Nombre)
                    .Set(a => a.Prefix, apiKey.Prefix)
                    .Set(a => a.Hash, apiKey.Hash)
                    .Set(a => a.Activa, apiKey.Activa)
                    .Set(a => a.RevocadaAt, apiKey.RevocadaAt)
                    .Set(a => a.RegeneradaAt, apiKey.RegeneradaAt),
                ct);
        }
        catch (MongoException ex) when (MongoErrores.EsClaveDuplicada(ex))
        {
            throw new ApiKeyPrefijoDuplicadoException();
        }

        if (resultado.MatchedCount == 0)
        {
            throw new ApiKeyNoEncontradaException();
        }
    }

    public async Task<bool> ActualizarUltimoUsoAsync(
        Guid id,
        Guid contribuyenteId,
        DateTime ahoraUtc,
        TimeSpan intervalo,
        CancellationToken ct = default)
    {
        DateTime? umbral = ahoraUtc - intervalo;

        var resultado = await _sesion.UpdateOneAsync(
            _context.ApiKeys,
            Filtro.Eq(a => a.Id, id)
                & Filtro.Eq(a => a.ContribuyenteId, contribuyenteId)
                & (Filtro.Eq(a => a.UltimoUso, (DateTime?)null) | Filtro.Lt(a => a.UltimoUso, umbral)),
            Update.Set(a => a.UltimoUso, ahoraUtc),
            ct);

        return resultado.MatchedCount > 0;
    }
}
