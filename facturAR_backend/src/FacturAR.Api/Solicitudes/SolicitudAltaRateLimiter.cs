using System.Threading.RateLimiting;

namespace FacturAR.Api.Solicitudes;

public sealed class SolicitudAltaRateLimiter : IDisposable
{
    private readonly PartitionedRateLimiter<string> _porIp;
    private readonly PartitionedRateLimiter<string> _porEmail;

    public SolicitudAltaRateLimiter(SolicitudAltaRateLimitOptions options)
    {
        _porIp = PartitionedRateLimiter.Create<string, string>(clave =>
            RateLimitPartition.GetFixedWindowLimiter(clave, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = options.PorIp,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

        _porEmail = PartitionedRateLimiter.Create<string, string>(clave =>
            RateLimitPartition.GetFixedWindowLimiter(clave, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = options.PorEmail,
                Window = TimeSpan.FromHours(1),
                QueueLimit = 0
            }));
    }

    public ValueTask<RateLimitLease> AdquirirPorIpAsync(string ip, CancellationToken ct)
        => _porIp.AcquireAsync(ip, cancellationToken: ct);

    public ValueTask<RateLimitLease> AdquirirPorEmailAsync(string email, CancellationToken ct)
        => _porEmail.AcquireAsync(email, cancellationToken: ct);

    public void Dispose()
    {
        _porIp.Dispose();
        _porEmail.Dispose();
    }
}
