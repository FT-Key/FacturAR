namespace FacturAR.Domain.Exceptions;

public sealed class ApiKeyValidationException : Exception
{
    public ApiKeyValidationException(string mensaje)
        : base(mensaje)
    {
    }
}
