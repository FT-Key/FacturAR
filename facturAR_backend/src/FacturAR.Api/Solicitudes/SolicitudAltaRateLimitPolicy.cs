using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace FacturAR.Api.Solicitudes;

public sealed class SolicitudAltaRateLimitPolicy : IRateLimiterPolicy<string>
{
    private readonly PartitionedRateLimiter<string> _porIp;
    private readonly PartitionedRateLimiter<string> _porEmail;
    private readonly bool _confiarEnProxy;

    public SolicitudAltaRateLimitPolicy(SolicitudAltaRateLimitOptions options)
    {
        _confiarEnProxy = options.ConfiarEnProxy;

        _porIp = PartitionedRateLimiter.Create<string, string>(ip =>
            RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = options.PorIp,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

        _porEmail = PartitionedRateLimiter.Create<string, string>(email =>
            RateLimitPartition.GetFixedWindowLimiter(email, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = options.PorEmail,
                Window = TimeSpan.FromHours(1),
                QueueLimit = 0
            }));
    }

    public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected
        => SolicitudAltaRateLimitRechazo.Handler;

    public RateLimitPartition<string> GetPartition(HttpContext httpContext)
    {
        var ip = ObtenerIp(httpContext);
        var email = httpContext.Items[SolicitudAltaRateLimitMiddleware.ItemKey] as string;
        var clave = email is null ? ip : $"{ip}|{email}";

        return RateLimitPartition.Get(
            clave,
            _ => new SolicitudAltaRateLimiter(_porIp, ip, _porEmail, email));
    }

    private string ObtenerIp(HttpContext context)
    {
        if (_confiarEnProxy)
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
