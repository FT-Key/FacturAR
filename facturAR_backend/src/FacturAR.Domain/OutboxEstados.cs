namespace FacturAR.Domain;

public static class OutboxEstados
{
    public const string Pendiente = "Pendiente";
    public const string Procesando = "Procesando";
    public const string Procesado = "Procesado";
    public const string Fallido = "Fallido";
}
