using FacturAR.Application.Abstractions;
using FacturAR.Domain.Entities;

namespace FacturAR.Application.Auth;

public sealed class RegistrarAccesoService
{
    private readonly IEventoAuditoriaRepository _eventos;

    public RegistrarAccesoService(IEventoAuditoriaRepository eventos)
    {
        _eventos = eventos;
    }

    public Task RegistrarLoginEmpresaAsync(Guid usuarioId, Guid contribuyenteId, CancellationToken ct = default)
        => _eventos.AddAsync(Evento(
            "LoginEmpresa",
            "Usuario",
            "Empresa",
            usuarioId.ToString(),
            contribuyenteId,
            usuarioId,
            null), ct);

    public Task RegistrarLoginAdminGlobalAsync(Guid administradorId, CancellationToken ct = default)
        => _eventos.AddAsync(Evento(
            "LoginAdminGlobal",
            "AdministradorFacturar",
            "Administrador",
            administradorId.ToString(),
            null,
            null,
            administradorId), ct);

    public Task RegistrarLogoutEmpresaAsync(Guid usuarioId, Guid contribuyenteId, CancellationToken ct = default)
        => _eventos.AddAsync(Evento(
            "LogoutEmpresa",
            "Usuario",
            "Empresa",
            usuarioId.ToString(),
            contribuyenteId,
            usuarioId,
            null), ct);

    public Task RegistrarLogoutAdminGlobalAsync(Guid administradorId, CancellationToken ct = default)
        => _eventos.AddAsync(Evento(
            "LogoutAdminGlobal",
            "AdministradorFacturar",
            "Administrador",
            administradorId.ToString(),
            null,
            null,
            administradorId), ct);

    public Task RegistrarLoginFallidoAsync(string email, CancellationToken ct = default)
        => _eventos.AddAsync(Evento(
            "LoginFallido",
            "Credencial",
            "Sistema",
            null,
            null,
            null,
            null), ct);

    public Task RegistrarAccesoDenegadoAsync(Guid? usuarioId, Guid? contribuyenteId, Guid? administradorId, CancellationToken ct = default)
        => _eventos.AddAsync(Evento(
            "AccesoDenegado",
            "Recurso",
            usuarioId.HasValue ? "Empresa" : "Administrador",
            null,
            contribuyenteId,
            usuarioId,
            administradorId), ct);

    public Task RegistrarAdminGlobalCreadoAsync(Guid administradorId, CancellationToken ct = default)
        => _eventos.AddAsync(Evento(
            "AdminGlobalCreado",
            "AdministradorFacturar",
            "Sistema",
            administradorId.ToString(),
            null,
            null,
            administradorId), ct);

    private static EventoAuditoria Evento(
        string accion,
        string entidad,
        string actorTipo,
        string? entidadId,
        Guid? contribuyenteId,
        Guid? usuarioId,
        Guid? administradorId)
        => new()
        {
            Accion = accion,
            Entidad = entidad,
            EntidadId = entidadId,
            ActorTipo = actorTipo,
            ContribuyenteId = contribuyenteId,
            UsuarioId = usuarioId,
            AdministradorId = administradorId
        };
}