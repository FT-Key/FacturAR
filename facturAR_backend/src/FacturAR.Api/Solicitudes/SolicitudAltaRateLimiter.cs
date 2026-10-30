using System.Threading.RateLimiting;

namespace FacturAR.Api.Solicitudes;

public sealed class SolicitudAltaRateLimiter : RateLimiter
{
    private readonly PartitionedRateLimiter<string> _porIp;
    private readonly string _ip;
    private readonly PartitionedRateLimiter<string> _porEmail;
    private readonly string? _email;

    public SolicitudAltaRateLimiter(
        PartitionedRateLimiter<string> porIp,
        string ip,
        PartitionedRateLimiter<string> porEmail,
        string? email)
    {
        _porIp = porIp;
        _ip = ip;
        _porEmail = porEmail;
        _email = email;
    }

    public override TimeSpan? IdleDuration => TimeSpan.FromHours(1);

    public override RateLimiterStatistics? GetStatistics() => null;

    protected override RateLimitLease AttemptAcquireCore(int permitCount)
    {
        var leaseIp = _porIp.AttemptAcquire(_ip, permitCount);
        if (!leaseIp.IsAcquired || _email is null)
        {
            return leaseIp;
        }

        var leaseEmail = _porEmail.AttemptAcquire(_email, permitCount);
        if (!leaseEmail.IsAcquired)
        {
            leaseIp.Dispose();
            return leaseEmail;
        }

        return new LeaseCombinado(leaseIp, leaseEmail);
    }

    protected override ValueTask<RateLimitLease> AcquireAsyncCore(
        int permitCount,
        CancellationToken cancellationToken)
        => AdquirirAsync(permitCount, cancellationToken);

    private async ValueTask<RateLimitLease> AdquirirAsync(
        int permitCount,
        CancellationToken cancellationToken)
    {
        var leaseIp = await _porIp.AcquireAsync(_ip, permitCount, cancellationToken);
        if (!leaseIp.IsAcquired || _email is null)
        {
            return leaseIp;
        }

        RateLimitLease leaseEmail;
        try
        {
            leaseEmail = await _porEmail.AcquireAsync(_email, permitCount, cancellationToken);
        }
        catch
        {
            leaseIp.Dispose();
            throw;
        }

        if (!leaseEmail.IsAcquired)
        {
            leaseIp.Dispose();
            return leaseEmail;
        }

        return new LeaseCombinado(leaseIp, leaseEmail);
    }

    private sealed class LeaseCombinado : RateLimitLease
    {
        private readonly RateLimitLease _ip;
        private readonly RateLimitLease _email;

        public LeaseCombinado(RateLimitLease ip, RateLimitLease email)
        {
            _ip = ip;
            _email = email;
        }

        public override bool IsAcquired => true;

        public override IEnumerable<string> MetadataNames
            => _ip.MetadataNames.Concat(_email.MetadataNames).Distinct(StringComparer.Ordinal);

        public override bool TryGetMetadata(string metadataName, out object? metadata)
        {
            if (_ip.TryGetMetadata(metadataName, out metadata))
            {
                return true;
            }

            return _email.TryGetMetadata(metadataName, out metadata);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _ip.Dispose();
                _email.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
