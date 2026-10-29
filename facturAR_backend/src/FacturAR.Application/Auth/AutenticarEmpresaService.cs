using FacturAR.Application.Abstractions;
using FacturAR.Contracts.Auth;

namespace FacturAR.Application.Auth;

public sealed class AutenticarEmpresaService
{
    private readonly IUsuarioRepository _usuarios;
    private readonly IContribuyenteRepository _contribuyentes;
    private readonly ILoginIntentStore _intentos;
    private readonly IPasswordHasher _hasher;
    private readonly AuthOptions _options;

    public AutenticarEmpresaService(
        IUsuarioRepository usuarios,
        IContribuyenteRepository contribuyentes,
        ILoginIntentStore intentos,
        IPasswordHasher hasher,
        AuthOptions options)
    {
        _usuarios = usuarios;
        _contribuyentes = contribuyentes;
        _intentos = intentos;
        _hasher = hasher;
        _options = options;
    }

    public async Task<SesionEmpresaResponse?> AutenticarAsync(string email, string password, CancellationToken ct = default)
    {
        var emailNormalizado = Normalizar(email);

        if (await _intentos.EstaBloqueadoAsync(emailNormalizado, ct))
        {
            return null;
        }

        var usuario = await _usuarios.GetByEmailAsync(emailNormalizado, ct);
        if (usuario is null || !usuario.Activo)
        {
            await _intentos.RegistrarFalloAsync(emailNormalizado, ct);
            return null;
        }

        if (!_hasher.Verify(password, usuario.PasswordHash))
        {
            await _intentos.RegistrarFalloAsync(emailNormalizado, ct);
            return null;
        }

        var contribuyente = await _contribuyentes.GetByIdAsync(usuario.ContribuyenteId, ct);
        if (contribuyente is null || !contribuyente.Activo)
        {
            await _intentos.RegistrarFalloAsync(emailNormalizado, ct);
            return null;
        }

        if (_hasher.NeedsRehash(usuario.PasswordHash))
        {
            usuario.PasswordHash = _hasher.Hash(password);
        }

        usuario.UltimoAcceso = DateTime.UtcNow;
        await _usuarios.UpdateAsync(usuario, ct);
        await _intentos.RegistrarExitoAsync(emailNormalizado, ct);

        return new SesionEmpresaResponse(
            usuario.Id,
            usuario.Email,
            usuario.Rol.ToString(),
            usuario.ContribuyenteId,
            contribuyente.RazonSocial,
            DateTime.UtcNow.AddHours(_options.Cookie.HorasSesionEmpresa));
    }

    private static string Normalizar(string email) => email.Trim().ToLowerInvariant();
}