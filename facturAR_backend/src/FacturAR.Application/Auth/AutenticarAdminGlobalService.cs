using FacturAR.Application.Abstractions;
using FacturAR.Contracts.Auth;

namespace FacturAR.Application.Auth;

public sealed class AutenticarAdminGlobalService
{
    private readonly IAdministradorRepository _administradores;
    private readonly ILoginIntentStore _intentos;
    private readonly IPasswordHasher _hasher;
    private readonly AuthOptions _options;

    public AutenticarAdminGlobalService(
        IAdministradorRepository administradores,
        ILoginIntentStore intentos,
        IPasswordHasher hasher,
        AuthOptions options)
    {
        _administradores = administradores;
        _intentos = intentos;
        _hasher = hasher;
        _options = options;
    }

    public async Task<SesionAdminResponse?> AutenticarAsync(string email, string password, CancellationToken ct = default)
    {
        var emailNormalizado = Normalizar(email);

        if (await _intentos.EstaBloqueadoAsync(emailNormalizado, ct))
        {
            return null;
        }

        var administrador = await _administradores.GetByEmailAsync(emailNormalizado, ct);
        if (administrador is null || !administrador.Activo)
        {
            await _intentos.RegistrarFalloAsync(emailNormalizado, ct);
            return null;
        }

        if (!_hasher.Verify(password, administrador.PasswordHash))
        {
            await _intentos.RegistrarFalloAsync(emailNormalizado, ct);
            return null;
        }

        if (_hasher.NeedsRehash(administrador.PasswordHash))
        {
            administrador.PasswordHash = _hasher.Hash(password);
        }

        administrador.UltimoAcceso = DateTime.UtcNow;
        await _administradores.UpdateAsync(administrador, ct);
        await _intentos.RegistrarExitoAsync(emailNormalizado, ct);

        return new SesionAdminResponse(
            administrador.Id,
            administrador.Email,
            administrador.Nombre,
            administrador.Rol.ToString(),
            DateTime.UtcNow.AddHours(_options.Cookie.HorasSesionAdmin));
    }

    private static string Normalizar(string email) => email.Trim().ToLowerInvariant();
}