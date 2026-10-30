namespace FacturAR.Api.Security;

public static class IpCliente
{
    public static string Obtener(HttpContext context, bool confiarEnProxy)
    {
        if (confiarEnProxy)
        {
            var reenviada = context.Request.Headers["X-Forwarded-For"].ToString();
            if (!string.IsNullOrWhiteSpace(reenviada))
            {
                var ultima = reenviada.Split(',')[^1].Trim();
                if (ultima.Length > 0)
                {
                    return ultima;
                }
            }
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "desconocida";
    }
}
