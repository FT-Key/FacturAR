using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using FacturAR.Application.Notificaciones;
using Microsoft.Extensions.Logging;

namespace FacturAR.Infrastructure.Notificaciones;

public sealed partial class FileEmailSender(EmailOptions options, ILogger<FileEmailSender> logger) : IEmailSender
{
    private static readonly Encoding Codificacion = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    public async Task<EmailResult> EnviarAsync(
        EmailMessage mensaje,
        CancellationToken cancellationToken = default)
    {
        var inicio = Stopwatch.GetTimestamp();
        var directorio = string.IsNullOrWhiteSpace(options.DirectorioSalida) ? "emails" : options.DirectorioSalida;
        var plantilla = PlantillaNoSegura().Replace(mensaje.Plantilla, "_");
        var nombre = $"{DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture)}_{plantilla}_{Guid.NewGuid():N}.html";
        var ruta = Path.Combine(directorio, nombre);

        try
        {
            Directory.CreateDirectory(directorio);
            await File.WriteAllTextAsync(ruta, Renderizar(mensaje), Codificacion, cancellationToken)
                .ConfigureAwait(false);

            var duracionMs = (long)Stopwatch.GetElapsedTime(inicio).TotalMilliseconds;
            logger.LogInformation(
                "Email escrito. plantilla={Plantilla} exito={Exito} archivo={Archivo} duracionMs={DuracionMs} destinatario={Destinatario}",
                mensaje.Plantilla,
                true,
                nombre,
                duracionMs,
                EmailMasker.Enmascarar(mensaje.Para));

            return EmailResult.Ok(DateTime.UtcNow);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            var duracionMs = (long)Stopwatch.GetElapsedTime(inicio).TotalMilliseconds;
            var error = $"Error al escribir el archivo ({ex.GetType().Name}).";
            logger.LogWarning(
                "Email no escrito. plantilla={Plantilla} exito={Exito} archivo={Archivo} duracionMs={DuracionMs} destinatario={Destinatario} error={Error}",
                mensaje.Plantilla,
                false,
                nombre,
                duracionMs,
                EmailMasker.Enmascarar(mensaje.Para),
                error);

            return EmailResult.Fallido(error);
        }
    }

    private static string Renderizar(EmailMessage mensaje)
    {
        var metadatos = $"""
            <!-- Plantilla: {LimpiarComentario(mensaje.Plantilla)} -->
            <!-- Asunto: {LimpiarComentario(mensaje.Asunto)} -->
            <!-- Para: {LimpiarComentario(mensaje.Para)} -->
            """;
        var html = NormalizarDocumento(mensaje.CuerpoHtml);
        var aperturaBody = html.IndexOf("<body", StringComparison.OrdinalIgnoreCase);

        if (aperturaBody >= 0)
        {
            var cierreApertura = html.IndexOf('>', aperturaBody);
            if (cierreApertura >= 0)
            {
                return string.Concat(
                    html[..(cierreApertura + 1)],
                    Environment.NewLine,
                    metadatos,
                    html[(cierreApertura + 1)..]);
            }
        }

        return $"""
            <!DOCTYPE html>
            <html lang="es">
            <head><meta charset="utf-8" /></head>
            <body>
            {metadatos}
            {html}
            </body>
            </html>
            """;
    }

    private static string NormalizarDocumento(string html)
    {
        var limpio = html.TrimStart();
        return limpio.StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase)
            ? limpio
            : $"<!DOCTYPE html>{Environment.NewLine}{html}";
    }

    private static string LimpiarComentario(string valor)
        => valor.Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Replace("--", "- -", StringComparison.Ordinal);

    [GeneratedRegex("[^A-Za-z0-9_-]", RegexOptions.CultureInvariant)]
    private static partial Regex PlantillaNoSegura();
}
