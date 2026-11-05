namespace FacturAR.Domain.Exceptions;

public sealed class IdempotenciaConflictoException : Exception
{
    public const string MensajePredeterminado = "La clave de idempotencia ya se usó con un payload diferente.";

    public IdempotenciaConflictoException()
        : base(MensajePredeterminado)
    {
    }
}
