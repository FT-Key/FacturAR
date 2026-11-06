namespace FacturAR.Api.Soap;

public sealed class FecaeFaultException : Exception
{
    public FecaeFaultException(int statusCode, string codigo, string mensaje)
        : base($"{codigo}: {mensaje}")
    {
        StatusCode = statusCode;
        Codigo = codigo;
        Mensaje = mensaje;
    }

    public int StatusCode { get; }

    public string Codigo { get; }

    public string Mensaje { get; }
}
