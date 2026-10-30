using FacturAR.Domain.Entities;

namespace FacturAR.Application.Abstractions;

public interface ISolicitudAltaRepository
{
    Task InsertarAsync(SolicitudAltaEmpresa solicitud, CancellationToken ct = default);

    Task<bool> ExistePendientePorCuitAsync(string cuit, CancellationToken ct = default);

    Task<bool> ExistePendientePorEmailAsync(string email, CancellationToken ct = default);
}
