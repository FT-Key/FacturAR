namespace FacturAR.Domain.Exceptions;

public sealed class UsuarioExistenteException : Exception
{
    public UsuarioExistenteException()
        : base("Ya existe un usuario con ese email en la empresa.")
    {
    }
}
