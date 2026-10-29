using FacturAR.Application.Abstractions;
using FacturAR.Domain.Entities;
using FacturAR.Domain.Enums;

namespace FacturAR.Application.Auth;

public sealed class AdminGlobalBootstrapService
{
    private readonly BootstrapAdminOptions _options;
    private readonly IAdministradorRepository _administradores;
    private readonly IPasswordHasher _hasher;
    private readonly RegistrarAccesoService _auditoria;

    public AdminGlobalBootstrapService(
        BootstrapAdminOptions options,
        IAdministradorRepository administradores,
        IPasswordHasher hasher,
        RegistrarAccesoService auditoria)
    {
        _options = options;
        _administradores = administradores;
        _hasher = hasher;
        _auditoria = auditoria;
    }

    public async Task EjecutarAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Email) || string.IsNullOrWhiteSpace(_options.Password))
        {
            return;
        }

        var email = _options.Email.Trim().ToLowerInvariant();

        if (await _administradores.ExistsByEmailAsync(email, ct))
        {
            return;
        }

        var administrador = new AdministradorFacturar
        {
            Nombre = string.IsNullOrWhiteSpace(_options.Nombre) ? "SuperAdmin" : _options.Nombre.Trim(),
            Email = email,
            PasswordHash = _hasher.Hash(_options.Password ?? string.Empty),
            Rol = RolGlobal.SuperAdmin,
            Activo = true
        };

        await _administradores.InsertAsync(administrador, ct);
        await _auditoria.RegistrarAdminGlobalCreadoAsync(administrador.Id, ct);
    }
}