namespace FacturAR.Domain.Exceptions;

public sealed class ImporteExcedidoException : Exception
{
    public ImporteExcedidoException(string mensaje)
        : base(mensaje)
    {
    }
}
