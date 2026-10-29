namespace FacturAR.Application.Notificaciones;

public sealed record EmailResult(bool Exitoso, DateTime? EnviadoAt, string? Error)
{
    public static EmailResult Ok(DateTime enviadoAt) => new(true, enviadoAt, null);

    public static EmailResult Fallido(string error) => new(false, null, error);
}
