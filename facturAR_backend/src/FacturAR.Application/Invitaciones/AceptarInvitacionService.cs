using System.Text.Json;
using FacturAR.Application.Abstractions;
using FacturAR.Contracts.Invitaciones;
using FacturAR.Domain.Entities;
using FacturAR.Domain.Exceptions;
using FacturAR.Domain.Validation;

namespace FacturAR.Application.Invitaciones;

public sealed class AceptarInvitacionService
{
    public const string MensajeCuentaActivada = "Cuenta activada. Ya podés iniciar sesión.";
    public const string MensajeConfirmacion = "Las contraseñas no coinciden.";
    public const string AccionCuentaActivada = "cuenta.activada";

    private readonly IInvitacionRepository _invitaciones;
    private readonly IContribuyenteRepository _contribuyentes;
    private readonly IUsuarioRepository _usuarios;
    private readonly IEventoAuditoriaRepository _eventos;
    private readonly IPasswordHasher _hasher;
    private readonly ITransaccionMongo _transaccion;

    public AceptarInvitacionService(
        IInvitacionRepository invitaciones,
        IContribuyenteRepository contribuyentes,
        IUsuarioRepository usuarios,
        IEventoAuditoriaRepository eventos,
        IPasswordHasher hasher,
        ITransaccionMongo transaccion)
    {
        _invitaciones = invitaciones;
        _contribuyentes = contribuyentes;
        _usuarios = usuarios;
        _eventos = eventos;
        _hasher = hasher;
        _transaccion = transaccion;
    }

    public async Task<AceptarInvitacionResponse> AceptarAsync(
        string? token,
        AceptarInvitacionRequest request,
        CancellationToken ct = default)
    {
        var (invitacion, _) = await InvitacionVigente.ResolverAsync(
            token,
            _invitaciones,
            _contribuyentes,
            DateTime.UtcNow,
            ct);

        var error = PoliticaContrasena.Validar(request.Password);
        if (error is not null)
        {
            throw new PasswordDebilException(error);
        }

        if (!string.Equals(request.Password, request.ConfirmacionPassword, StringComparison.Ordinal))
        {
            throw new SolicitudAltaValidationException(MensajeConfirmacion);
        }

        var passwordHash = _hasher.Hash(request.Password);

        try
        {
            await _transaccion.EjecutarAsync(
                async innerCt =>
                {
                    var ahora = DateTime.UtcNow;
                    if (await _usuarios.ExistePorContribuyenteYEmailAsync(invitacion.ContribuyenteId, invitacion.Email, innerCt))
                    {
                        throw new UsuarioExistenteException();
                    }

                    if (!await _invitaciones.MarcarUsadaAsync(invitacion.Id, ahora, innerCt))
                    {
                        throw new InvitacionUsadaException();
                    }

                    var usuario = new Usuario
                    {
                        ContribuyenteId = invitacion.ContribuyenteId,
                        Email = invitacion.Email,
                        PasswordHash = passwordHash,
                        Rol = invitacion.Rol,
                        Activo = true,
                        EmailVerificadoAt = ahora,
                        CreatedAt = ahora
                    };
                    await _usuarios.InsertAsync(usuario, innerCt);

                    await _eventos.AddAsync(
                        new EventoAuditoria
                        {
                            Accion = AccionCuentaActivada,
                            Entidad = nameof(Invitacion),
                            EntidadId = invitacion.Id.ToString(),
                            ContribuyenteId = invitacion.ContribuyenteId,
                            UsuarioId = usuario.Id,
                            ActorTipo = "Empresa",
                            Datos = JsonSerializer.Serialize(new Dictionary<string, string>
                            {
                                ["rol"] = usuario.Rol.ToString(),
                                ["solicitudId"] = invitacion.SolicitudId?.ToString() ?? string.Empty
                            }),
                            CreatedAt = ahora
                        },
                        innerCt);

                    return usuario.Id;
                },
                ct);
        }
        catch (UsuarioExistenteException)
        {
            throw new InvitacionUsadaException();
        }

        return new AceptarInvitacionResponse(MensajeCuentaActivada);
    }
}
