using FacturAR.Domain.Entities;

namespace FacturAR.Application.Abstractions;

public interface ISeedRepository
{
    Task<Contribuyente> UpsertContribuyenteAsync(Contribuyente contribuyente, CancellationToken ct = default);

    Task UpsertUsuarioAsync(Usuario usuario, CancellationToken ct = default);

    Task UpsertAdministradorAsync(AdministradorFacturar administrador, CancellationToken ct = default);

    Task UpsertPuntoVentaAsync(PuntoVenta puntoVenta, CancellationToken ct = default);

    Task UpsertSecuenciaAsync(Secuencia secuencia, CancellationToken ct = default);

    Task UpsertParametroAsync(Parametro parametro, CancellationToken ct = default);

    Task<bool> CrearApiKeySiNoExisteAsync(ApiKey apiKey, CancellationToken ct = default);
}
