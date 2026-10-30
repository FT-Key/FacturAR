using System.Globalization;
using System.Net.Mail;
using System.Text.Json;
using System.Threading.RateLimiting;
using FacturAR.Contracts.Comunes;

namespace FacturAR.Api.Solicitudes;

public sealed class SolicitudAltaRateLimitMiddleware
{
    private const int LimiteLecturaBytes = 16 * 1024;
    private const int EmailMaximo = 254;
    private const int SegundosReintentoPorDefecto = 60;
    private const string MensajeRateLimit = "Demasiadas solicitudes. Intentá de nuevo más tarde.";

    private readonly RequestDelegate _next;
    private readonly SolicitudAltaRateLimiter _limiter;
    private readonly bool _confiarEnProxy;

    public SolicitudAltaRateLimitMiddleware(
        RequestDelegate next,
        SolicitudAltaRateLimiter limiter,
        SolicitudAltaRateLimitOptions options)
    {
        _next = next;
        _limiter = limiter;
        _confiarEnProxy = options.ConfiarEnProxy;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var ip = ObtenerIp(context);
        var email = await LeerEmailAsync(context);

        using var leaseIp = await _limiter.AdquirirPorIpAsync(ip, context.RequestAborted);
        if (!leaseIp.IsAcquired)
        {
            await RechazarAsync(context, leaseIp);
            return;
        }

        using var leaseEmail = await _limiter.AdquirirPorEmailAsync(email ?? ip, context.RequestAborted);
        if (!leaseEmail.IsAcquired)
        {
            await RechazarAsync(context, leaseEmail);
            return;
        }

        await _next(context);
    }

    private static async Task RechazarAsync(HttpContext context, RateLimitLease lease)
    {
        var segundos = lease.TryGetMetadata(MetadataName.RetryAfter, out var reintento)
            ? (int)Math.Ceiling(reintento.TotalSeconds)
            : SegundosReintentoPorDefecto;

        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.Headers["Retry-After"] = segundos.ToString(CultureInfo.InvariantCulture);
        await context.Response.WriteAsJsonAsync(
            new ErrorResponse("RATE_LIMIT", MensajeRateLimit),
            context.RequestAborted);
    }

    private string ObtenerIp(HttpContext context)
    {
        if (_confiarEnProxy)
        {
            var reenviada = context.Request.Headers["X-Forwarded-For"].ToString();
            if (!string.IsNullOrWhiteSpace(reenviada))
            {
                var primera = reenviada.Split(',')[0].Trim();
                if (primera.Length > 0)
                {
                    return primera;
                }
            }
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "desconocida";
    }

    private static async Task<string?> LeerEmailAsync(HttpContext context)
    {
        context.Request.EnableBuffering();

        try
        {
            var buffer = new byte[LimiteLecturaBytes];
            var leidos = 0;

            while (leidos < buffer.Length)
            {
                var lectura = await context.Request.Body.ReadAsync(
                    buffer.AsMemory(leidos, buffer.Length - leidos),
                    context.RequestAborted);

                if (lectura == 0)
                {
                    break;
                }

                leidos += lectura;
            }

            return ExtraerEmail(buffer.AsSpan(0, leidos));
        }
        finally
        {
            context.Request.Body.Position = 0;
        }
    }

    private static string? ExtraerEmail(ReadOnlySpan<byte> cuerpo)
    {
        if (cuerpo.IsEmpty)
        {
            return null;
        }

        try
        {
            using var documento = JsonDocument.Parse(cuerpo.ToArray());
            if (documento.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            string? email = null;
            var encontrado = false;

            foreach (var propiedad in documento.RootElement.EnumerateObject())
            {
                if (!string.Equals(propiedad.Name, "email", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                encontrado = true;
                email = propiedad.Value.ValueKind == JsonValueKind.String
                    ? propiedad.Value.GetString()
                    : null;
            }

            if (encontrado)
            {
                return NormalizarEmail(email);
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private static string? NormalizarEmail(string? email)
    {
        var normalizado = (email ?? string.Empty).Trim().ToLowerInvariant();
        if (normalizado.Length is 0 or > EmailMaximo)
        {
            return null;
        }

        return MailAddress.TryCreate(normalizado, out var direccion)
            && direccion is not null
            && string.Equals(direccion.Address, normalizado, StringComparison.OrdinalIgnoreCase)
                ? normalizado
                : null;
    }
}
