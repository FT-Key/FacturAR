namespace FacturAR.Domain.Exceptions;

public sealed class ApiKeyPrefijoDuplicadoException : Exception
{
    public ApiKeyPrefijoDuplicadoException()
        : base("El prefijo de la API key ya está en uso.")
    {
    }
}
