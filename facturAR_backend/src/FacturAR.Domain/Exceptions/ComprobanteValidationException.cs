namespace FacturAR.Domain.Exceptions;

public sealed class ComprobanteValidationException : Exception
{
    public ComprobanteValidationException(string mensaje)
        : base(mensaje)
    {
    }
}
