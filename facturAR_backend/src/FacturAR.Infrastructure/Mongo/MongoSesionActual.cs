using MongoDB.Driver;

namespace FacturAR.Infrastructure.Mongo;

public sealed class MongoSesionActual
{
    public IClientSessionHandle? Sesion { get; set; }

    public IFindFluent<T, T> Find<T>(IMongoCollection<T> coleccion, FilterDefinition<T> filtro)
        => Sesion is null ? coleccion.Find(filtro) : coleccion.Find(Sesion, filtro);

    public Task InsertOneAsync<T>(IMongoCollection<T> coleccion, T documento, CancellationToken ct)
        => Sesion is null
            ? coleccion.InsertOneAsync(documento, cancellationToken: ct)
            : coleccion.InsertOneAsync(Sesion, documento, cancellationToken: ct);

    public Task<UpdateResult> UpdateOneAsync<T>(
        IMongoCollection<T> coleccion,
        FilterDefinition<T> filtro,
        UpdateDefinition<T> update,
        CancellationToken ct)
        => Sesion is null
            ? coleccion.UpdateOneAsync(filtro, update, cancellationToken: ct)
            : coleccion.UpdateOneAsync(Sesion, filtro, update, cancellationToken: ct);

    public async Task<T?> FindOneAndUpdateAsync<T>(
        IMongoCollection<T> coleccion,
        FilterDefinition<T> filtro,
        UpdateDefinition<T> update,
        FindOneAndUpdateOptions<T> opciones,
        CancellationToken ct = default)
    {
        var sesion = Sesion
            ?? throw new InvalidOperationException("La operación requiere una transacción activa.");

        return await coleccion.FindOneAndUpdateAsync(sesion, filtro, update, opciones, ct);
    }
}
