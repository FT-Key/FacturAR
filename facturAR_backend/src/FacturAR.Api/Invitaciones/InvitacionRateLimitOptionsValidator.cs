namespace FacturAR.Api.Invitaciones;

public static class InvitacionRateLimitOptionsValidator
{
    public static void Validar(InvitacionRateLimitOptions options)
    {
        if (options.ValidacionesPorIp <= 0)
        {
            throw new InvalidOperationException("RateLimit:Invitacion:ValidacionesPorIp debe ser mayor a 0.");
        }

        if (options.ActivacionesPorIp <= 0)
        {
            throw new InvalidOperationException("RateLimit:Invitacion:ActivacionesPorIp debe ser mayor a 0.");
        }
    }
}
