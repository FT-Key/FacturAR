namespace FacturAR.Domain.Exceptions;

public sealed class SolicitudYaProcesadaException : Exception
{
    public SolicitudYaProcesadaException()
        : base("La solicitud ya fue resuelta.")
    {
    }
}
