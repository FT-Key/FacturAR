namespace FacturAR.Domain.Exceptions;

public sealed class PuntoVentaNoEncontradoException : Exception
{
    public PuntoVentaNoEncontradoException()
        : base("El punto de venta no existe o no pertenece al contribuyente autenticado.")
    {
    }
}
