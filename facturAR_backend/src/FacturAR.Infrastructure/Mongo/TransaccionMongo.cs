using FacturAR.Application.Abstractions;
using MongoDB.Driver;

namespace FacturAR.Infrastructure.Mongo;

public sealed class TransaccionMongo : ITransaccionMongo
{
    private readonly IMongoClient _client;
    private readonly MongoSesionActual _sesionActual;

    public TransaccionMongo(IMongoClient client, MongoSesionActual sesionActual)
    {
        _client = client;
        _sesionActual = sesionActual;
    }

    public async Task<T> EjecutarAsync<T>(Func<CancellationToken, Task<T>> operacion, CancellationToken ct = default)
    {
        if (_sesionActual.Sesion is not null)
        {
            throw new InvalidOperationException("Ya hay una transacción de MongoDB en curso en este scope.");
        }

        using var sesion = await _client.StartSessionAsync(cancellationToken: ct);
        _sesionActual.Sesion = sesion;
        try
        {
            return await sesion.WithTransactionAsync(
                (_, token) => operacion(token),
                cancellationToken: ct);
        }
        finally
        {
            _sesionActual.Sesion = null;
        }
    }
}
