namespace FacturAR.Domain.Exceptions;

public sealed class InvitacionNoEncontradaException : Exception
{
    public const string MensajeActivacion = "El link de activación no es válido.";

    public InvitacionNoEncontradaException()
        : base("No hay una invitación para reenviar.")
    {
    }

    public InvitacionNoEncontradaException(string mensaje)
        : base(mensaje)
    {
    }
}
