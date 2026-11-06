namespace FacturAR.Domain.Exceptions;

public sealed class ApiKeyNoEncontradaException : Exception
{
    public ApiKeyNoEncontradaException()
        : base("La API key no existe para esta empresa.")
    {
    }
}
