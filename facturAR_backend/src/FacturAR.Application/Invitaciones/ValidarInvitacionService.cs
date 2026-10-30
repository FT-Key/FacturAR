using FacturAR.Application.Abstractions;
using FacturAR.Contracts.Invitaciones;

namespace FacturAR.Application.Invitaciones;

public sealed class ValidarInvitacionService
{
    private readonly IInvitacionRepository _invitaciones;
    private readonly IContribuyenteRepository _contribuyentes;

    public ValidarInvitacionService(IInvitacionRepository invitaciones, IContribuyenteRepository contribuyentes)
    {
        _invitaciones = invitaciones;
        _contribuyentes = contribuyentes;
    }

    public async Task<InvitacionValidacionResponse> ValidarAsync(string? token, CancellationToken ct = default)
    {
        var (invitacion, contribuyente) = await InvitacionVigente.ResolverAsync(
            token,
            _invitaciones,
            _contribuyentes,
            DateTime.UtcNow,
            ct);

        return new InvitacionValidacionResponse(
            contribuyente.RazonSocial,
            InvitacionVigente.EnmascararEmail(invitacion.Email),
            invitacion.ExpiraAt);
    }
}
