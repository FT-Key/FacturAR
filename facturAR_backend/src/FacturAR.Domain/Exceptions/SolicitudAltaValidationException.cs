namespace FacturAR.Domain.Exceptions;

public sealed class SolicitudAltaValidationException : Exception
{
    public SolicitudAltaValidationException(string mensaje)
        : base(mensaje)
    {
    }
}
