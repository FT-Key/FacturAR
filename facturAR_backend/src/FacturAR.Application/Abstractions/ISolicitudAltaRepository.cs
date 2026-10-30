using FacturAR.Domain.Entities;
using FacturAR.Domain.Enums;

namespace FacturAR.Application.Abstractions;

public sealed record FiltroSolicitudes(
    EstadoSolicitud? Estado,
    string? Cuit,
    string? RazonSocial,
    string? Email,
    int Page,
    int PageSize);

public interface ISolicitudAltaRepository
{
    Task InsertarAsync(SolicitudAltaEmpresa solicitud, CancellationToken ct = default);

    Task<bool> ExistePendientePorCuitAsync(string cuit, CancellationToken ct = default);

    Task<bool> ExistePendientePorEmailAsync(string email, CancellationToken ct = default);

    Task<SolicitudAltaEmpresa?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default);

    Task<(IReadOnlyList<SolicitudAltaEmpresa> Items, long Total)> ListarAsync(
        FiltroSolicitudes filtro,
        CancellationToken ct = default);

    Task<bool> ActualizarDatosPendienteAsync(SolicitudAltaEmpresa solicitud, CancellationToken ct = default);

    Task<bool> AprobarPendienteAsync(
        Guid id,
        Guid contribuyenteId,
        Guid invitacionId,
        Guid administradorId,
        DateTime revisadaAt,
        CancellationToken ct = default);

    Task<bool> RechazarPendienteAsync(
        Guid id,
        string motivo,
        Guid administradorId,
        DateTime revisadaAt,
        CancellationToken ct = default);
}
