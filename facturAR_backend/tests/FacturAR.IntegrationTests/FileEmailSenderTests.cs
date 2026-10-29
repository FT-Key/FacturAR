using System.Text.RegularExpressions;
using FacturAR.Application.Notificaciones;
using FacturAR.Infrastructure.Notificaciones;

namespace FacturAR.IntegrationTests;

public sealed class FileEmailSenderTests
{
    [Fact]
    public async Task EnviarAsync_EscribeUnDocumentoHtmlCompletoSinLoguearElToken()
    {
        var directorio = Path.Combine(Path.GetTempPath(), $"facturar_email_{Guid.NewGuid():N}");
        const string token = "token-super-secreto-123";
        var logger = new RecordingLogger<FileEmailSender>();
        var sender = new FileEmailSender(new EmailOptions { DirectorioSalida = directorio }, logger);
        var plantilla = PlantillasEmail.NombreSolicitudAprobada;
        var mensaje = new EmailMessage(
            "juan@dominio.com",
            "Invitación de acceso",
            $"<!DOCTYPE html><html lang=\"es\"><head><meta charset=\"utf-8\" /><title>Invitación</title></head><body><a href=\"http://localhost:5116/activar?token={token}\">Activar</a></body></html>",
            $"Activá tu cuenta: http://localhost:5116/activar?token={token}",
            plantilla);

        var resultado = await sender.EnviarAsync(mensaje);

        try
        {
            Assert.True(resultado.Exitoso);
            Assert.Null(resultado.Error);

            var archivo = Assert.Single(Directory.GetFiles(directorio, "*.html"));
            var patron = new Regex(
                $@"^\d{{8}}-\d{{6}}-\d{{3}}_{Regex.Escape(plantilla)}_[0-9a-f]{{32}}\.html$");
            Assert.Matches(patron, Path.GetFileName(archivo));

            var contenido = (await File.ReadAllTextAsync(archivo)).TrimStart();
            Assert.StartsWith("<!DOCTYPE html>", contenido);
            Assert.Contains("<html", contenido);
            Assert.Contains("</body>", contenido);
            Assert.Contains("</html>", contenido);
            Assert.Contains(token, contenido);
            Assert.Contains("Invitación de acceso", contenido);
            Assert.Contains("juan@dominio.com", contenido);

            Assert.NotEmpty(logger.Messages);
            Assert.All(logger.Messages, m => Assert.DoesNotContain(token, m));
            Assert.All(logger.Messages, m => Assert.DoesNotContain("Activá tu cuenta", m));
            Assert.All(logger.Messages, m => Assert.DoesNotContain("juan@dominio.com", m));
            Assert.All(logger.Messages, m => Assert.DoesNotContain(directorio, m));
            Assert.Contains(logger.Messages, m => m.Contains("j***@dominio.com", StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(directorio))
            {
                Directory.Delete(directorio, recursive: true);
            }
        }
    }

    [Fact]
    public async Task EnviarAsync_SaneaLaPlantillaEnElNombreDelArchivo()
    {
        var directorio = Path.Combine(Path.GetTempPath(), $"facturar_email_{Guid.NewGuid():N}");

        try
        {
            var sender = new FileEmailSender(
                new EmailOptions { DirectorioSalida = directorio },
                new RecordingLogger<FileEmailSender>());
            var mensaje = new EmailMessage(
                "juan@dominio.com",
                "Asunto",
                "<html lang=\"es\"><body>cuerpo</body></html>",
                "cuerpo",
                "Plantilla/invalida con espacios:á");

            var resultado = await sender.EnviarAsync(mensaje);

            Assert.True(resultado.Exitoso);
            var archivo = Assert.Single(Directory.GetFiles(directorio));
            var nombre = Path.GetFileName(archivo);
            Assert.Matches(new Regex(@"^\d{8}-\d{6}-\d{3}_[A-Za-z0-9_-]+_[0-9a-f]{32}\.html$"), nombre);
            Assert.Contains("Plantilla_invalida_con_espacios__", nombre, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(directorio))
            {
                Directory.Delete(directorio, recursive: true);
            }
        }
    }

    [Fact]
    public async Task EnviarAsync_ConCancelacionSolicitada_PropagaLaCancelacion()
    {
        var directorio = Path.Combine(Path.GetTempPath(), $"facturar_email_{Guid.NewGuid():N}");

        try
        {
            var sender = new FileEmailSender(
                new EmailOptions { DirectorioSalida = directorio },
                new RecordingLogger<FileEmailSender>());
            var mensaje = new EmailMessage(
                "juan@dominio.com",
                "Asunto",
                "<html lang=\"es\"><body>cuerpo</body></html>",
                "cuerpo",
                "SolicitudRecibida");
            using var cancellationTokenSource = new CancellationTokenSource();
            await cancellationTokenSource.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => sender.EnviarAsync(mensaje, cancellationTokenSource.Token));
        }
        finally
        {
            if (Directory.Exists(directorio))
            {
                Directory.Delete(directorio, recursive: true);
            }
        }
    }

    [Fact]
    public async Task EnviarAsync_SiNoPuedeEscribir_DevuelveFallidoSinPropagar()
    {
        var archivo = Path.Combine(Path.GetTempPath(), $"facturar_email_{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(archivo, "x");

        try
        {
            var sender = new FileEmailSender(
                new EmailOptions { DirectorioSalida = archivo },
                new RecordingLogger<FileEmailSender>());
            var mensaje = new EmailMessage(
                "juan@dominio.com",
                "Asunto",
                "<html lang=\"es\"><body>cuerpo</body></html>",
                "cuerpo",
                "SolicitudRecibida");

            var resultado = await sender.EnviarAsync(mensaje);

            Assert.False(resultado.Exitoso);
            Assert.NotNull(resultado.Error);
            Assert.DoesNotContain("cuerpo", resultado.Error);
        }
        finally
        {
            File.Delete(archivo);
        }
    }
}
