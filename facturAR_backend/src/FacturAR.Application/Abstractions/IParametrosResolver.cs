namespace FacturAR.Application.Abstractions;

public interface IParametrosResolver
{
    Task<string?> ObtenerAsync(string clave, Guid? contribuyenteId, CancellationToken ct = default);

    Task<int> ObtenerDiasVencimientoCaeAsync(Guid? contribuyenteId, CancellationToken ct = default);

    Task<IReadOnlyList<decimal>> ObtenerAlicuotasPermitidasAsync(Guid? contribuyenteId, CancellationToken ct = default);

    Task<string> ObtenerMonedaAsync(Guid? contribuyenteId, CancellationToken ct = default);
}
