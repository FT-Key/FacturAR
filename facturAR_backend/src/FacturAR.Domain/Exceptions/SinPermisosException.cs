namespace FacturAR.Domain.Exceptions;

public sealed class SinPermisosException : Exception
{
    public SinPermisosException(string mensaje)
        : base(mensaje)
    {
    }
}
