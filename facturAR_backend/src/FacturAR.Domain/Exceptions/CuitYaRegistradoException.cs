namespace FacturAR.Domain.Exceptions;

public sealed class CuitYaRegistradoException : Exception
{
    public CuitYaRegistradoException()
        : base("El CUIT ya está en uso por otra empresa.")
    {
    }
}
