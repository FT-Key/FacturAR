namespace FacturAR.Application.Abstractions;

public interface ITransaccionMongo
{
    Task<T> EjecutarAsync<T>(Func<CancellationToken, Task<T>> operacion, CancellationToken ct = default);
}
