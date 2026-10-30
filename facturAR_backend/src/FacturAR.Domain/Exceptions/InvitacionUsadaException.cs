namespace FacturAR.Domain.Exceptions;

public sealed class InvitacionUsadaException : Exception
{
    public InvitacionUsadaException()
        : base("El link de activación ya fue usado.")
    {
    }
}
