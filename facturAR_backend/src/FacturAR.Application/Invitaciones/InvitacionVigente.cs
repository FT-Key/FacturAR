using FacturAR.Application.Abstractions;
using FacturAR.Domain.Entities;
using FacturAR.Domain.Enums;
using FacturAR.Domain.Exceptions;
using FacturAR.Domain.ValueObjects;

namespace FacturAR.Application.Invitaciones;

public static class InvitacionVigente
{
    private const int LargoMaximoToken = 128;

    public static async Task<(Invitacion Invitacion, Contribuyente Contribuyente)> ResolverAsync(
        string? token,
        IInvitacionRepository invitaciones,
        IContribuyenteRepository contribuyentes,
        DateTime ahora,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > LargoMaximoToken)
        {
            throw new InvitacionNoEncontradaException(InvitacionNoEncontradaException.MensajeActivacion);
        }

        var invitacion = await invitaciones.ObtenerPorTokenHashAsync(TokenInvitacion.CalcularHash(token.Trim()), ct);
        if (invitacion is null || invitacion.Estado == EstadoInvitacion.Revocada)
        {
            throw new InvitacionNoEncontradaException(InvitacionNoEncontradaException.MensajeActivacion);
        }

        switch (invitacion.EstadoEfectivo(ahora))
        {
            case EstadoInvitacion.Usada:
                throw new InvitacionUsadaException();
            case EstadoInvitacion.Vencida:
                throw new InvitacionVencidaException();
        }

        var contribuyente = await contribuyentes.GetByIdAsync(invitacion.ContribuyenteId, ct);
        if (contribuyente is null || !contribuyente.Activo)
        {
            throw new CuentaInactivaException();
        }

        return (invitacion, contribuyente);
    }

    public static string EnmascararEmail(string email)
    {
        var arroba = email.IndexOf('@');
        if (arroba == 1 && arroba < email.Length - 1)
        {
            return $"***{email[arroba..]}";
        }

        if (arroba <= 0 || arroba == email.Length - 1)
        {
            return email.Length == 0 ? "***" : $"{email[0]}***";
        }

        return $"{email[0]}***{email[arroba..]}";
    }
}
