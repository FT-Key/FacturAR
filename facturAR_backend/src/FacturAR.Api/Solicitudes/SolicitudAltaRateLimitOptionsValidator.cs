namespace FacturAR.Api.Solicitudes;

public static class SolicitudAltaRateLimitOptionsValidator
{
    public static void Validar(SolicitudAltaRateLimitOptions options)
    {
        if (options.PorIp <= 0)
        {
            throw new InvalidOperationException("RateLimit:SolicitudAlta:PorIp debe ser mayor a 0.");
        }

        if (options.PorEmail <= 0)
        {
            throw new InvalidOperationException("RateLimit:SolicitudAlta:PorEmail debe ser mayor a 0.");
        }
    }
}
