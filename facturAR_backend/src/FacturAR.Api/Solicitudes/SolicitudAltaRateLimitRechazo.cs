using System.Globalization;
using System.Threading.RateLimiting;
using FacturAR.Contracts.Comunes;
using Microsoft.AspNetCore.RateLimiting;

namespace FacturAR.Api.Solicitudes;

public static class SolicitudAltaRateLimitRechazo
{
    public const string Mensaje = "Demasiadas solicitudes. Intentá de nuevo más tarde.";

    private const int SegundosReintentoPorDefecto = 60;

    public static Func<OnRejectedContext, CancellationToken, ValueTask> Handler { get; } = RechazarAsync;

    private static async ValueTask RechazarAsync(
        OnRejectedContext context,
        CancellationToken cancellationToken)
    {
        if (context.HttpContext.Response.HasStarted)
        {
            return;
        }

        var segundos = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var reintento)
            ? (int)Math.Ceiling(reintento.TotalSeconds)
            : SegundosReintentoPorDefecto;

        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.Headers["Retry-After"] = segundos.ToString(CultureInfo.InvariantCulture);
        await context.HttpContext.Response.WriteAsJsonAsync(
            new ErrorResponse("RATE_LIMIT", Mensaje),
            cancellationToken);
    }
}
