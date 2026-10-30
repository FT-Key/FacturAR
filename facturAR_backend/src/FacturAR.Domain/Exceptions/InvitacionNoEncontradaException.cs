namespace FacturAR.Domain.Exceptions;

public sealed class InvitacionNoEncontradaException : Exception
{
    public InvitacionNoEncontradaException()
        : base("No hay una invitación para reenviar.")
    {
    }
}
