namespace FacturAR.Domain.Exceptions;

public sealed class PasswordDebilException : Exception
{
    public PasswordDebilException(string mensaje)
        : base(mensaje)
    {
    }
}
