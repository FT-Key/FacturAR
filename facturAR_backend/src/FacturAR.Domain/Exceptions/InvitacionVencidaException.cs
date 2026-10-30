namespace FacturAR.Domain.Exceptions;

public sealed class InvitacionVencidaException : Exception
{
    public InvitacionVencidaException()
        : base("El link de activación venció. Pedí que te reenvíen la invitación.")
    {
    }
}
