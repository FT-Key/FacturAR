using System.Globalization;
using System.Net;
using FacturAR.Domain.Entities;

namespace FacturAR.Application.Notificaciones;

public sealed record ContenidoEmail(string Plantilla, string Asunto, string Html, string Texto);

public static class PlantillasEmail
{
    public const string NombreSolicitudRecibida = "SolicitudRecibida";
    public const string NombreNuevaSolicitud = "NuevaSolicitud";
    public const string NombreSolicitudAprobada = "SolicitudAprobada";
    public const string NombreSolicitudRechazada = "SolicitudRechazada";
    public const string NombreInvitacionReenviada = "InvitacionReenviada";

    public static ContenidoEmail CrearSolicitudRecibida(SolicitudAltaEmpresa solicitud)
    {
        var html = Layout("Solicitud recibida", $"""
            <p>Hola {Codificar(solicitud.NombreSolicitante)},</p>
            <p>Recibimos tu solicitud de acceso a FacturAR para <strong>{Codificar(solicitud.RazonSocial)}</strong>.</p>
            <p>La revisión es manual. Si tu solicitud es aprobada, el próximo contacto llegará por email.</p>
            <p>No compartas este mensaje: no contiene credenciales de acceso.</p>
            """);
        var texto = Texto(
            $"Hola {solicitud.NombreSolicitante},",
            $"Recibimos tu solicitud de acceso a FacturAR para {solicitud.RazonSocial}.",
            "La revisión es manual. Si tu solicitud es aprobada, el próximo contacto llegará por email.",
            "No compartas este mensaje: no contiene credenciales de acceso.");

        return new ContenidoEmail(
            NombreSolicitudRecibida,
            "Recibimos tu solicitud de acceso a FacturAR",
            html,
            texto);
    }

    public static ContenidoEmail CrearNuevaSolicitud(SolicitudAltaEmpresa solicitud, string portalBaseUrl)
    {
        var panel = $"{portalBaseUrl.TrimEnd('/')}/admin/solicitudes";
        var html = Layout("Nueva solicitud de alta", $"""
            <p>Se recibió una nueva solicitud de alta de empresa.</p>
            <ul>
            <li>Razón social: {Codificar(solicitud.RazonSocial)}</li>
            <li>CUIT: {Codificar(solicitud.Cuit)}</li>
            <li>Email del solicitante: {Codificar(solicitud.EmailSolicitante)}</li>
            <li>Fecha de recepción: {Codificar(Fecha(solicitud.CreatedAt))}</li>
            </ul>
            <p><a href="{Codificar(panel)}">Revisar solicitudes en el panel</a></p>
            """);
        var texto = Texto(
            "Se recibió una nueva solicitud de alta de empresa.",
            $"Razón social: {solicitud.RazonSocial}",
            $"CUIT: {solicitud.Cuit}",
            $"Email del solicitante: {solicitud.EmailSolicitante}",
            $"Fecha de recepción: {Fecha(solicitud.CreatedAt)}",
            $"Panel: {panel}");

        return new ContenidoEmail(
            NombreNuevaSolicitud,
            $"Nueva solicitud de alta: {solicitud.RazonSocial}",
            html,
            texto);
    }

    public static ContenidoEmail CrearSolicitudAprobada(
        SolicitudAltaEmpresa solicitud,
        string linkActivacion,
        DateTime expiraAt)
    {
        var html = Layout("Solicitud aprobada", $"""
            <p>Hola {Codificar(solicitud.NombreSolicitante)},</p>
            <p>Tu solicitud de acceso a FacturAR para <strong>{Codificar(solicitud.RazonSocial)}</strong> fue aprobada.</p>
            <p>Activá tu cuenta desde el siguiente link:</p>
            <p><a href="{Codificar(linkActivacion)}">Activar mi cuenta</a></p>
            <p>El link vence el {Codificar(Fecha(expiraAt))}{Codificar(Vigencia(expiraAt))} y es de un solo uso.</p>
            """);
        var texto = Texto(
            $"Hola {solicitud.NombreSolicitante},",
            $"Tu solicitud de acceso a FacturAR para {solicitud.RazonSocial} fue aprobada.",
            "Activá tu cuenta desde el siguiente link:",
            linkActivacion,
            $"El link vence el {Fecha(expiraAt)}{Vigencia(expiraAt)} y es de un solo uso.");

        return new ContenidoEmail(
            NombreSolicitudAprobada,
            "Tu solicitud fue aprobada: activá tu cuenta",
            html,
            texto);
    }

    public static ContenidoEmail CrearSolicitudRechazada(SolicitudAltaEmpresa solicitud)
    {
        var motivo = solicitud.MotivoRechazo ?? string.Empty;
        var html = Layout("Solicitud rechazada", $"""
            <p>Hola {Codificar(solicitud.NombreSolicitante)},</p>
            <p>Tu solicitud de acceso a FacturAR para <strong>{Codificar(solicitud.RazonSocial)}</strong> fue rechazada.</p>
            <p>Motivo: {Codificar(motivo)}</p>
            """);
        var texto = Texto(
            $"Hola {solicitud.NombreSolicitante},",
            $"Tu solicitud de acceso a FacturAR para {solicitud.RazonSocial} fue rechazada.",
            $"Motivo: {motivo}");

        return new ContenidoEmail(
            NombreSolicitudRechazada,
            "Tu solicitud de acceso fue rechazada",
            html,
            texto);
    }

    public static ContenidoEmail CrearInvitacionReenviada(
        SolicitudAltaEmpresa solicitud,
        string linkActivacion,
        DateTime expiraAt)
    {
        var html = Layout("Invitación reenviada", $"""
            <p>Hola {Codificar(solicitud.NombreSolicitante)},</p>
            <p>Reenviamos tu invitación de acceso a FacturAR para <strong>{Codificar(solicitud.RazonSocial)}</strong>.</p>
            <p>Activá tu cuenta desde el siguiente link:</p>
            <p><a href="{Codificar(linkActivacion)}">Activar mi cuenta</a></p>
            <p>El link vence el {Codificar(Fecha(expiraAt))}{Codificar(Vigencia(expiraAt))} y es de un solo uso. El link anterior queda sin efecto.</p>
            """);
        var texto = Texto(
            $"Hola {solicitud.NombreSolicitante},",
            $"Reenviamos tu invitación de acceso a FacturAR para {solicitud.RazonSocial}.",
            "Activá tu cuenta desde el siguiente link:",
            linkActivacion,
            $"El link vence el {Fecha(expiraAt)}{Vigencia(expiraAt)} y es de un solo uso. El link anterior queda sin efecto.");

        return new ContenidoEmail(
            NombreInvitacionReenviada,
            "Reenviamos tu invitación de acceso a FacturAR",
            html,
            texto);
    }

    private static string Layout(string titulo, string contenidoHtml) => $"""
        <!DOCTYPE html>
        <html lang="es">
        <head>
        <meta charset="utf-8" />
        <title>{Codificar(titulo)}</title>
        </head>
        <body style="margin:0;padding:24px;background-color:#f5f5f5;font-family:Arial,Helvetica,sans-serif;color:#1f2937;">
        <div style="max-width:600px;margin:0 auto;padding:24px;background-color:#ffffff;border-radius:8px;">
        <h1 style="font-size:20px;margin-top:0;">{Codificar(titulo)}</h1>
        {contenidoHtml}
        </div>
        <p style="max-width:600px;margin:16px auto 0;font-size:12px;color:#6b7280;">FacturAR</p>
        </body>
        </html>
        """;

    private static string Codificar(string valor) => WebUtility.HtmlEncode(valor);

    private static string Texto(params string[] lineas) => string.Join("\r\n", lineas);

    private static string Fecha(DateTime valor)
        => valor.ToUniversalTime().ToString("dd/MM/yyyy HH:mm 'UTC'", CultureInfo.InvariantCulture);

    private static string Vigencia(DateTime expiraAt)
    {
        var horas = (int)Math.Round(
            (expiraAt.ToUniversalTime() - DateTime.UtcNow).TotalHours,
            MidpointRounding.AwayFromZero);

        return horas > 0 ? $" ({horas} horas)" : string.Empty;
    }
}
