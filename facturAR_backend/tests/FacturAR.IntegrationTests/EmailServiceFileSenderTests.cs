using FacturAR.Application.Notificaciones;
using FacturAR.Domain.Entities;
using FacturAR.Infrastructure.Notificaciones;

namespace FacturAR.IntegrationTests;

public sealed class EmailServiceFileSenderTests
{
    [Fact]
    public async Task SolicitudRecibida_GeneraArchivoConProximoPasoYLogueaMetadatos()
    {
        var directorio = NuevoDirectorioTemporal();

        try
        {
            var logger = new RecordingLogger<FileEmailSender>();
            var service = CrearService(logger, directorio);
            var solicitud = CrearSolicitud();

            var resultado = await service.EnviarSolicitudRecibidaAsync(solicitud, CancellationToken.None);

            Assert.True(resultado.Exitoso);
            Assert.Null(resultado.Error);

            var archivo = Assert.Single(Directory.GetFiles(directorio, "*.html"));
            var contenido = await File.ReadAllTextAsync(archivo);

            Assert.Contains("Recibimos tu solicitud de acceso a FacturAR", contenido);
            Assert.Contains("ana@acme.com", contenido);
            Assert.Contains("ACME S.A.", contenido);
            Assert.Contains("revisión es manual", contenido);
            Assert.Contains("próximo contacto", contenido);

            Assert.Contains(logger.Messages, m => m.Contains("plantilla=SolicitudRecibida", StringComparison.Ordinal));
            Assert.Contains(logger.Messages, m => m.Contains("exito=True", StringComparison.Ordinal));
            Assert.Contains(logger.Messages, m => m.Contains("archivo=", StringComparison.Ordinal));
            Assert.Contains(logger.Messages, m => m.Contains("destinatario=a***@acme.com", StringComparison.Ordinal));
        }
        finally
        {
            EliminarDirectorio(directorio);
        }
    }

    [Fact]
    public async Task SolicitudAprobada_EscribeElLinkCompletoSinLoguearElToken()
    {
        const string token = "token-super-secreto-123";
        var directorio = NuevoDirectorioTemporal();

        try
        {
            var logger = new RecordingLogger<FileEmailSender>();
            var service = CrearService(logger, directorio);

            var resultado = await service.EnviarSolicitudAprobadaAsync(
                CrearSolicitud(),
                token,
                DateTime.UtcNow.AddHours(48),
                CancellationToken.None);

            Assert.True(resultado.Exitoso);

            var archivo = Assert.Single(Directory.GetFiles(directorio, "*.html"));
            var contenido = await File.ReadAllTextAsync(archivo);
            var link = $"http://localhost:5116/activar?token={token}";

            Assert.Contains(link, contenido);
            Assert.Contains("48 horas", contenido);
            Assert.NotEmpty(logger.Messages);
            Assert.All(logger.Messages, m => Assert.DoesNotContain(token, m, StringComparison.Ordinal));
            Assert.All(logger.Messages, m => Assert.DoesNotContain("Activá tu cuenta", m, StringComparison.Ordinal));
        }
        finally
        {
            EliminarDirectorio(directorio);
        }
    }

    [Fact]
    public async Task SolicitudRechazada_ConservaElMotivoEscapadoEnElArchivo()
    {
        var directorio = NuevoDirectorioTemporal();

        try
        {
            var logger = new RecordingLogger<FileEmailSender>();
            var service = CrearService(logger, directorio);
            var solicitud = CrearSolicitud();
            solicitud.MotivoRechazo = "Datos <script>alert(1)</script> incompletos";

            var resultado = await service.EnviarSolicitudRechazadaAsync(solicitud, CancellationToken.None);

            Assert.True(resultado.Exitoso);

            var archivo = Assert.Single(Directory.GetFiles(directorio, "*.html"));
            var contenido = await File.ReadAllTextAsync(archivo);

            Assert.Contains("&lt;script&gt;", contenido);
            Assert.DoesNotContain("<script>", contenido);
            Assert.All(logger.Messages, m => Assert.DoesNotContain("<script>", m, StringComparison.Ordinal));
        }
        finally
        {
            EliminarDirectorio(directorio);
        }
    }

    private static EmailService CrearService(RecordingLogger<FileEmailSender> logger, string directorio)
        => new(
            new FileEmailSender(new EmailOptions { DirectorioSalida = directorio }, logger),
            new NotificacionesOptions
            {
                PortalBaseUrl = "http://localhost:5116",
                AdminNotificationEmail = null
            });

    private static SolicitudAltaEmpresa CrearSolicitud() => new()
    {
        RazonSocial = "ACME S.A.",
        Cuit = "20123456786",
        NombreSolicitante = "Ana Pérez",
        EmailSolicitante = "ana@acme.com",
        CreatedAt = new DateTime(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc)
    };

    private static string NuevoDirectorioTemporal()
        => Path.Combine(Path.GetTempPath(), $"facturar_email_{Guid.NewGuid():N}");

    private static void EliminarDirectorio(string directorio)
    {
        if (Directory.Exists(directorio))
        {
            Directory.Delete(directorio, recursive: true);
        }
    }
}
