using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using FacturAR.Web.Services.Asistente;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.Extensions.Options;

namespace FacturAR.Web.Endpoints;

public static class AsistenteEndpoints
{
    private const int MaximoCuerpoBytes = 8 * 1024;
    private const string NombreCookieSesion = "facturar-asistente";

    private static readonly Regex SesionValida = new("^[A-Za-z0-9_-]{32,64}$", RegexOptions.Compiled);

    private static readonly JsonSerializerOptions OpcionesJson = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static IEndpointRouteBuilder MapAsistenteEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/asistente/mensajes", async (
            HttpContext context,
            AsistenteAltasService servicio,
            IAntiforgery antiforgery,
            IOptions<AsistenteOptions> opcionesAcceso,
            IConfiguration configuracion) =>
        {
            var opciones = opcionesAcceso.Value;

            if (!opciones.Habilitado)
            {
                return Results.NotFound();
            }

            if (!OrigenValido(context, opciones, configuracion))
            {
                return Results.Json(
                    new AsistenteErrorResponse("ORIGEN_INVALIDO", "No se pudo validar el origen de la solicitud."),
                    statusCode: StatusCodes.Status403Forbidden);
            }

            try
            {
                await antiforgery.ValidateRequestAsync(context);
            }
            catch (AntiforgeryValidationException)
            {
                return Validacion("No se pudo validar la solicitud.");
            }

            if (!EsJson(context.Request.ContentType))
            {
                return Validacion("El cuerpo debe enviarse como application/json.");
            }

            string cuerpo;
            try
            {
                cuerpo = await LeerCuerpoAsync(context.Request, context.RequestAborted);
            }
            catch (CuerpoExcedidoException)
            {
                return Validacion("El mensaje es demasiado grande.");
            }

            AsistenteMensajeRequest? solicitud;
            try
            {
                solicitud = JsonSerializer.Deserialize<AsistenteMensajeRequest>(cuerpo, OpcionesJson);
            }
            catch (JsonException)
            {
                return Validacion("La solicitud no es válida.");
            }

            if (solicitud is null)
            {
                return Validacion("La solicitud no es válida.");
            }

            var sesion = ObtenerSesion(context);
            var ip = context.Connection.RemoteIpAddress?.ToString() ?? "desconocida";

            try
            {
                var respuesta = await servicio.ResponderAsync(solicitud, ip, sesion, context.RequestAborted);
                return Results.Json(respuesta);
            }
            catch (AsistenteValidacionException ex)
            {
                return Validacion(ex.Message);
            }
            catch (AsistenteLimiteException ex)
            {
                var segundos = Math.Max(1, (int)Math.Ceiling(ex.RetryAfter.TotalSeconds));
                context.Response.Headers.RetryAfter = segundos.ToString(CultureInfo.InvariantCulture);
                return Results.Json(
                    new AsistenteErrorResponse(
                        "ASISTENTE_LIMITE",
                        "Hiciste varios intentos seguidos. Esperá un momento y volvé a probar."),
                    statusCode: StatusCodes.Status429TooManyRequests);
            }
            catch (AsistenteNoDisponibleException)
            {
                return Results.Json(
                    new AsistenteErrorResponse("ASISTENTE_NO_DISPONIBLE", AsistentePolitica.MensajeFallback),
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        });

        return app;
    }

    private static IResult Validacion(string mensaje)
        => Results.Json(
            new AsistenteErrorResponse("VALIDACION", mensaje),
            statusCode: StatusCodes.Status400BadRequest);

    private static bool EsJson(string? contentType)
        => contentType is not null
            && contentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase);

    private static bool OrigenValido(HttpContext context, AsistenteOptions opciones, IConfiguration configuracion)
    {
        var configurado = string.IsNullOrWhiteSpace(opciones.OrigenPermitido)
            ? configuracion["Portal:BaseUrl"]
            : opciones.OrigenPermitido;

        var permitido = (string.IsNullOrWhiteSpace(configurado)
                ? $"{context.Request.Scheme}://{context.Request.Host}"
                : configurado)
            .TrimEnd('/');

        var origen = context.Request.Headers.Origin.ToString();
        if (!string.IsNullOrWhiteSpace(origen))
        {
            return string.Equals(origen.TrimEnd('/'), permitido, StringComparison.OrdinalIgnoreCase);
        }

        var referer = context.Request.Headers.Referer.ToString();
        if (!string.IsNullOrWhiteSpace(referer)
            && Uri.TryCreate(referer, UriKind.Absolute, out var uri))
        {
            var origenReferer = $"{uri.Scheme}://{uri.Authority}";
            return string.Equals(origenReferer, permitido, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static async Task<string> LeerCuerpoAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        if (request.ContentLength is > MaximoCuerpoBytes)
        {
            throw new CuerpoExcedidoException();
        }

        using var memoria = new MemoryStream();
        var buffer = new byte[4096];
        int leidos;

        while ((leidos = await request.Body.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (memoria.Length + leidos > MaximoCuerpoBytes)
            {
                throw new CuerpoExcedidoException();
            }

            memoria.Write(buffer, 0, leidos);
        }

        return Encoding.UTF8.GetString(memoria.ToArray());
    }

    private static string ObtenerSesion(HttpContext context)
    {
        if (context.Request.Cookies.TryGetValue(NombreCookieSesion, out var existente)
            && existente is not null
            && SesionValida.IsMatch(existente))
        {
            return existente;
        }

        var valor = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        context.Response.Cookies.Append(NombreCookieSesion, valor, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = context.Request.IsHttps,
            IsEssential = true,
            Path = "/"
        });

        return valor;
    }

    private sealed class CuerpoExcedidoException : Exception
    {
    }
}
