namespace FacturAR.Application.Comprobantes;

public sealed class IdempotenciaDuplicadaException : Exception
{
    public IdempotenciaDuplicadaException(Exception? innerException = null)
        : base("Ya existe un comprobante con la misma clave de idempotencia.", innerException)
    {
    }
}
