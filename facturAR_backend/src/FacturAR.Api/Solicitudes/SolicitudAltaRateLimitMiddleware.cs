using System.Net.Mail;
using System.Text.Json;
using FacturAR.Contracts.Comunes;

namespace FacturAR.Api.Solicitudes;

public sealed class SolicitudAltaRateLimitMiddleware
{
    public const string ItemKey = "SolicitudAlta.Email";

    private const int LimiteLecturaBytes = 32 * 1024;
    private const int EmailMaximo = 254;
    private const string MensajeValidacion = "El cuerpo de la solicitud no es válido.";

    private readonly RequestDelegate _next;

    public SolicitudAltaRateLimitMiddleware(RequestDelegate next)
        => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var (valido, email) = await LeerEmailAsync(context);

        if (!valido)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(
                new ErrorResponse("VALIDACION", MensajeValidacion),
                context.RequestAborted);
            return;
        }

        if (email is not null)
        {
            context.Items[ItemKey] = email;
        }

        await _next(context);
    }

    private static async Task<(bool Valido, string? Email)> LeerEmailAsync(HttpContext context)
    {
        context.Request.EnableBuffering();

        try
        {
            var buffer = new byte[LimiteLecturaBytes + 1];
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

            if (leidos > LimiteLecturaBytes)
            {
                return (false, null);
            }

            return ExtraerEmail(buffer.AsSpan(0, leidos));
        }
        finally
        {
            context.Request.Body.Position = 0;
        }
    }

    private static (bool Valido, string? Email) ExtraerEmail(ReadOnlySpan<byte> cuerpo)
    {
        if (cuerpo.IsEmpty)
        {
            return (false, null);
        }

        try
        {
            using var documento = JsonDocument.Parse(cuerpo.ToArray());
            if (documento.RootElement.ValueKind != JsonValueKind.Object)
            {
                return (false, null);
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

            return (true, encontrado ? NormalizarEmail(email) : null);
        }
        catch (JsonException)
        {
            return (false, null);
        }
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
