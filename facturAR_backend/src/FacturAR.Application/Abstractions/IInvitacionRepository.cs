using FacturAR.Domain.Entities;

namespace FacturAR.Application.Abstractions;

public interface IInvitacionRepository
{
    Task InsertarAsync(Invitacion invitacion, CancellationToken ct = default);

    Task<Invitacion?> ObtenerPorTokenHashAsync(string tokenHash, CancellationToken ct = default);

    Task<Invitacion?> ObtenerVigentePorSolicitudAsync(Guid solicitudId, CancellationToken ct = default);

    Task<bool> MarcarUsadaAsync(Guid id, DateTime usadaAt, CancellationToken ct = default);

    Task<bool> RevocarAsync(Guid id, DateTime revocadaAt, CancellationToken ct = default);

    Task RegistrarEnvioAsync(Guid id, DateTime enviadoAt, CancellationToken ct = default);
}
