namespace FacturAR.Domain.Exceptions;

public sealed class PuntoVentaInactivoException : Exception
{
    public PuntoVentaInactivoException()
        : base("El punto de venta está inactivo.")
    {
    }
}
