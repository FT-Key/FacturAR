namespace FacturAR.Domain.Exceptions;

public sealed class ComprobanteAnuladoException : Exception
{
    public ComprobanteAnuladoException(string mensaje)
        : base(mensaje)
    {
    }
}
