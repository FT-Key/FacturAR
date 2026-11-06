using FacturAR.Application.Abstractions;
using FacturAR.Application.Auditoria;
using FacturAR.Domain;
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
            AccionesAuditoria.LoginExitoso,
            EntidadesAuditoria.Usuario,
            ActorTipos.Empresa,
            usuarioId.ToString(),
            contribuyenteId,
            usuarioId,
            null), ct);

    public Task RegistrarLoginAdminGlobalAsync(Guid administradorId, CancellationToken ct = default)
        => _eventos.AddAsync(Evento(
            AccionesAuditoria.LoginExitoso,
            EntidadesAuditoria.AdministradorFacturar,
            ActorTipos.Administrador,
            administradorId.ToString(),
            null,
            null,
            administradorId), ct);

    public Task RegistrarLogoutEmpresaAsync(Guid usuarioId, Guid contribuyenteId, CancellationToken ct = default)
        => _eventos.AddAsync(Evento(
            AccionesAuditoria.Logout,
            EntidadesAuditoria.Usuario,
            ActorTipos.Empresa,
            usuarioId.ToString(),
            contribuyenteId,
            usuarioId,
            null), ct);

    public Task RegistrarLogoutAdminGlobalAsync(Guid administradorId, CancellationToken ct = default)
        => _eventos.AddAsync(Evento(
            AccionesAuditoria.Logout,
            EntidadesAuditoria.AdministradorFacturar,
            ActorTipos.Administrador,
            administradorId.ToString(),
            null,
            null,
            administradorId), ct);

    public Task RegistrarLoginFallidoAsync(CancellationToken ct = default)
        => _eventos.AddAsync(Evento(
            AccionesAuditoria.LoginFallido,
            EntidadesAuditoria.Usuario,
            ActorTipos.Sistema,
            null,
            null,
            null,
            null), ct);

    public Task RegistrarAccesoDenegadoAsync(Guid? usuarioId, Guid? contribuyenteId, Guid? administradorId, CancellationToken ct = default)
    {
        if (usuarioId.HasValue)
        {
            if (!contribuyenteId.HasValue)
            {
                throw new ArgumentException(
                    "Un acceso denegado de usuario requiere contribuyenteId.",
                    nameof(contribuyenteId));
            }

            return _eventos.AddAsync(Evento(
                AccionesAuditoria.AccesoDenegado,
                EntidadesAuditoria.Recurso,
                ActorTipos.Empresa,
                null,
                contribuyenteId,
                usuarioId,
                null), ct);
        }

        if (administradorId.HasValue)
        {
            return _eventos.AddAsync(Evento(
                AccionesAuditoria.AccesoDenegado,
                EntidadesAuditoria.Recurso,
                ActorTipos.Administrador,
                null,
                null,
                null,
                administradorId), ct);
        }

        return _eventos.AddAsync(Evento(
            AccionesAuditoria.AccesoDenegado,
            EntidadesAuditoria.Recurso,
            ActorTipos.Sistema,
            null,
            null,
            null,
            null), ct);
    }

    public Task RegistrarAdminGlobalCreadoAsync(Guid administradorId, CancellationToken ct = default)
        => _eventos.AddAsync(Evento(
            AccionesAuditoria.AdminGlobalCreado,
            EntidadesAuditoria.AdministradorFacturar,
            ActorTipos.Sistema,
            administradorId.ToString(),
            null,
            null,
            null), ct);

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
