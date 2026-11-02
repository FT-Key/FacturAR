namespace FacturAR.Domain.Exceptions;

public sealed class EmisionInvalidaException : Exception
{
    public EmisionInvalidaException(string mensaje)
        : base(mensaje)
    {
    }
}
