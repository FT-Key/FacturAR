namespace FacturAR.Domain.Exceptions;

public sealed class SolicitudPendienteException : Exception
{
    public SolicitudPendienteException()
        : base("Ya existe una solicitud pendiente con el mismo CUIT o email.")
    {
    }
}
