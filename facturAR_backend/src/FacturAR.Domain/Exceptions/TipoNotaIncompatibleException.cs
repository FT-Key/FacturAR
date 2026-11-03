namespace FacturAR.Domain.Exceptions;

public sealed class TipoNotaIncompatibleException : Exception
{
    public TipoNotaIncompatibleException(string mensaje)
        : base(mensaje)
    {
    }
}
