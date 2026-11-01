namespace FacturAR.Application.Abstractions;

public interface IParametrosResolver
{
    Task<string?> ObtenerAsync(string clave, Guid? contribuyenteId, CancellationToken ct = default);
}
