using FacturAR.Domain.Entities;

namespace FacturAR.Application.Abstractions;

public interface IPuntoVentaRepository
{
    Task InsertarAsync(PuntoVenta puntoVenta, CancellationToken ct = default);
}
