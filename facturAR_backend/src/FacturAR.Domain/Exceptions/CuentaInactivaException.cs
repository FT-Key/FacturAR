namespace FacturAR.Domain.Exceptions;

public sealed class CuentaInactivaException : Exception
{
    public CuentaInactivaException()
        : base("La empresa no está habilitada para activar cuentas.")
    {
    }
}
