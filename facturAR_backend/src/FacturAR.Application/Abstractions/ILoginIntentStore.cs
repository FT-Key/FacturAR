namespace FacturAR.Application.Abstractions;

public interface ILoginIntentStore
{
    Task RegistrarFalloAsync(string email, CancellationToken ct = default);
    Task<bool> EstaBloqueadoAsync(string email, CancellationToken ct = default);
    Task RegistrarExitoAsync(string email, CancellationToken ct = default);
}