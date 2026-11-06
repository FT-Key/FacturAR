namespace FacturAR.Domain.Exceptions;

public sealed class ComprobanteNoEncontradoException : Exception
{
    public ComprobanteNoEncontradoException(string mensaje)
        : base(mensaje)
    {
    }
}
