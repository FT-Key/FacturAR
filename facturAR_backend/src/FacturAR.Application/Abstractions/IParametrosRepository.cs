namespace FacturAR.Application.Abstractions;

public interface IParametrosRepository
{
    Task<string?> ObtenerValorAsync(Guid contribuyenteId, string clave, CancellationToken ct = default);
}
