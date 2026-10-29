using System.Globalization;
using FacturAR.Application.Notificaciones;
using FacturAR.Domain.Entities;

namespace FacturAR.UnitTests;

public sealed class PlantillasEmailTests
{
    [Fact]
    public void CrearSolicitudRecibida_UsaAsuntoYNoIncluyeCuit()
    {
        var solicitud = CrearSolicitud();

        var contenido = PlantillasEmail.CrearSolicitudRecibida(solicitud);

        Assert.Equal("Recibimos tu solicitud de acceso a FacturAR", contenido.Asunto);
        Assert.Equal(PlantillasEmail.NombreSolicitudRecibida, contenido.Plantilla);
        Assert.Contains("revisión es manual", contenido.Html);
        Assert.Contains("revisión es manual", contenido.Texto);
        Assert.DoesNotContain(solicitud.Cuit, contenido.Html);
        Assert.DoesNotContain(solicitud.Cuit, contenido.Texto);
        Assert.Contains("\r\n", contenido.Texto);
    }

    [Fact]
    public void CrearNuevaSolicitud_IncluyeAsuntoConRazonSocialYLinkAlPanel()
    {
        var solicitud = CrearSolicitud();

        var contenido = PlantillasEmail.CrearNuevaSolicitud(solicitud, "http://localhost:5116");

        Assert.Equal($"Nueva solicitud de alta: {solicitud.RazonSocial}", contenido.Asunto);
        Assert.Equal(PlantillasEmail.NombreNuevaSolicitud, contenido.Plantilla);
        Assert.Contains("http://localhost:5116/admin/solicitudes", contenido.Html);
        Assert.Contains("http://localhost:5116/admin/solicitudes", contenido.Texto);
        Assert.Contains(solicitud.Cuit, contenido.Html);
        Assert.Contains(solicitud.Cuit, contenido.Texto);
    }

    [Fact]
    public void CrearSolicitudAprobada_IncluyeLinkYFechaDeExpiracion()
    {
        var solicitud = CrearSolicitud();
        var expiraAt = DateTime.UtcNow.AddHours(48);
        var fechaEsperada = expiraAt.ToString("dd/MM/yyyy HH:mm 'UTC'", CultureInfo.InvariantCulture);

        var contenido = PlantillasEmail.CrearSolicitudAprobada(
            solicitud,
            "http://localhost:5116/activar?token=abc123",
            expiraAt);

        Assert.Equal("Tu solicitud fue aprobada: activá tu cuenta", contenido.Asunto);
        Assert.Equal(PlantillasEmail.NombreSolicitudAprobada, contenido.Plantilla);
        Assert.Contains("http://localhost:5116/activar?token=abc123", contenido.Html);
        Assert.Contains("http://localhost:5116/activar?token=abc123", contenido.Texto);
        Assert.Contains("48 horas", contenido.Html);
        Assert.Contains("48 horas", contenido.Texto);
        Assert.Contains(fechaEsperada, contenido.Html);
        Assert.Contains(fechaEsperada, contenido.Texto);
        Assert.Contains("un solo uso", contenido.Html);
        Assert.Contains("un solo uso", contenido.Texto);
    }

    [Fact]
    public void CrearSolicitudRechazada_EscapaElMotivoEnHtmlYLoConservaEnTexto()
    {
        var solicitud = CrearSolicitud();
        solicitud.MotivoRechazo = "Datos <script>alert(1)</script> incompletos";

        var contenido = PlantillasEmail.CrearSolicitudRechazada(solicitud);

        Assert.Equal("Tu solicitud de acceso fue rechazada", contenido.Asunto);
        Assert.Equal(PlantillasEmail.NombreSolicitudRechazada, contenido.Plantilla);
        Assert.Contains("&lt;script&gt;", contenido.Html);
        Assert.DoesNotContain("<script>", contenido.Html);
        Assert.Contains("Datos <script>alert(1)</script> incompletos", contenido.Texto);
    }

    [Fact]
    public void CrearInvitacionReenviada_IncluyeLinkNuevoYAvisoDelAnterior()
    {
        var solicitud = CrearSolicitud();
        var expiraAt = DateTime.UtcNow.AddHours(48);

        var contenido = PlantillasEmail.CrearInvitacionReenviada(
            solicitud,
            "http://localhost:5116/activar?token=nuevo",
            expiraAt);

        Assert.Equal("Reenviamos tu invitación de acceso a FacturAR", contenido.Asunto);
        Assert.Equal(PlantillasEmail.NombreInvitacionReenviada, contenido.Plantilla);
        Assert.Contains("http://localhost:5116/activar?token=nuevo", contenido.Html);
        Assert.Contains("http://localhost:5116/activar?token=nuevo", contenido.Texto);
        Assert.Contains("anterior queda sin efecto", contenido.Html);
        Assert.Contains("anterior queda sin efecto", contenido.Texto);
        Assert.Contains("48 horas", contenido.Texto);
    }

    [Fact]
    public void Plantillas_NoContienenEtiquetasEnElTextoPlano()
    {
        var solicitud = CrearSolicitud();
        var expiraAt = new DateTime(2026, 10, 31, 10, 30, 0, DateTimeKind.Utc);

        var contenidos = new[]
        {
            PlantillasEmail.CrearSolicitudRecibida(solicitud),
            PlantillasEmail.CrearNuevaSolicitud(solicitud, "http://localhost:5116"),
            PlantillasEmail.CrearSolicitudAprobada(solicitud, "http://localhost:5116/activar?token=abc", expiraAt),
            PlantillasEmail.CrearSolicitudRechazada(solicitud),
            PlantillasEmail.CrearInvitacionReenviada(solicitud, "http://localhost:5116/activar?token=abc", expiraAt)
        };

        Assert.All(contenidos, contenido =>
        {
            Assert.DoesNotContain("<p>", contenido.Texto);
            Assert.DoesNotContain("<html", contenido.Texto);
            Assert.Contains("\r\n", contenido.Texto);
            Assert.Contains("FacturAR", contenido.Html);
        });
    }

    [Fact]
    public void Plantillas_RenderizanHtmlYTextoConElMismoContenidoSinSecretos()
    {
        var solicitud = CrearSolicitud();
        var expiraAt = DateTime.UtcNow.AddHours(48);
        var contenidos = new[]
        {
            PlantillasEmail.CrearSolicitudRecibida(solicitud),
            PlantillasEmail.CrearNuevaSolicitud(solicitud, "http://localhost:5116"),
            PlantillasEmail.CrearSolicitudAprobada(solicitud, "http://localhost:5116/activar?token=abc", expiraAt),
            PlantillasEmail.CrearSolicitudRechazada(solicitud),
            PlantillasEmail.CrearInvitacionReenviada(solicitud, "http://localhost:5116/activar?token=abc", expiraAt)
        };

        Assert.Equal(5, contenidos.Length);
        Assert.Equal(5, contenidos.Select(c => c.Plantilla).Distinct(StringComparer.Ordinal).Count());

        Assert.All(contenidos, contenido =>
        {
            Assert.False(string.IsNullOrWhiteSpace(contenido.Asunto));
            Assert.StartsWith("<!DOCTYPE html>", contenido.Html.TrimStart());
            Assert.Contains("</html>", contenido.Html);
            Assert.Contains("ACME S.A.", contenido.Html);
            Assert.Contains("ACME S.A.", contenido.Texto);
            Assert.Contains("\r\n", contenido.Texto);
            Assert.DoesNotContain("<", contenido.Texto, StringComparison.Ordinal);
            Assert.DoesNotContain("<script", contenido.Html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("javascript:", contenido.Html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("<link", contenido.Html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("<img", contenido.Html, StringComparison.OrdinalIgnoreCase);

            foreach (var secreto in new[] { "api key", "apikey", "password", "contraseña", "hash", "bearer" })
            {
                Assert.DoesNotContain(secreto, contenido.Html, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(secreto, contenido.Texto, StringComparison.OrdinalIgnoreCase);
            }
        });
    }

    [Fact]
    public void Plantillas_CodificanLaRazonSocialEnHtmlYLaConservanLiteralEnTexto()
    {
        var solicitud = CrearSolicitud();
        solicitud.RazonSocial = "ACME & Cia <S.A.>";

        var contenido = PlantillasEmail.CrearSolicitudRecibida(solicitud);

        Assert.Contains("ACME &amp; Cia &lt;S.A.&gt;", contenido.Html);
        Assert.DoesNotContain("<S.A.>", contenido.Html);
        Assert.Contains("ACME & Cia <S.A.>", contenido.Texto);
    }

    private static SolicitudAltaEmpresa CrearSolicitud() => new()
    {
        RazonSocial = "ACME S.A.",
        Cuit = "20123456786",
        NombreSolicitante = "Ana Pérez",
        EmailSolicitante = "ana@acme.com",
        CreatedAt = new DateTime(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc)
    };
}
