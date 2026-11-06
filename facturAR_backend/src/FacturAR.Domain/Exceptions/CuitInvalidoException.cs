namespace FacturAR.Domain.Exceptions;

public sealed class CuitInvalidoException : Exception
{
    public CuitInvalidoException(string mensaje)
        : base(mensaje)
    {
    }
}
