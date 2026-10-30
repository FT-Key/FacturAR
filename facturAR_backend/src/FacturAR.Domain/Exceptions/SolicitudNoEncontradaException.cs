namespace FacturAR.Domain.Exceptions;

public sealed class SolicitudNoEncontradaException : Exception
{
    public SolicitudNoEncontradaException()
        : base("No existe la solicitud indicada.")
    {
    }
}
