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

    private const string ColorPrimario = "#1D5E73";
    private const string ColorAcento = "#8F4E1C";
    private const string ColorTexto = "#101A20";
    private const string ColorTextoSuave = "#4F5E66";
    private const string ColorBorde = "#D8D4C8";
    private const string ColorFondo = "#F6F4EF";
    private const string ColorSuperficie = "#FFFFFF";
    private const string ColorExito = "#1F7A4D";
    private const string ColorPeligro = "#B3261E";
    private const string FuenteBase = "Arial,'Segoe UI',Helvetica,sans-serif";
    private const string FuenteTitulo = "Georgia,'Times New Roman',serif";

    public static ContenidoEmail CrearSolicitudRecibida(SolicitudAltaEmpresa solicitud)
    {
        var contenido = $"""
            <p style="margin:0 0 16px;">Hola {Codificar(solicitud.NombreSolicitante)},</p>
            <p style="margin:0 0 16px;">
              Recibimos tu solicitud de acceso a <strong>FacturAR</strong> para
              <strong>{Codificar(solicitud.RazonSocial)}</strong>.
            </p>
            {Aviso("La revisión es manual. Si tu solicitud es aprobada, el próximo contacto llegará por email.", ColorAcento)}
            <p style="margin:0 0 16px;">Podés seguir el avance del alta desde el portal cuando recibas la invitación.</p>
            <p style="margin:0;color:{ColorTextoSuave};font-size:13px;">
              No compartas este mensaje: no contiene credenciales de acceso.
            </p>
            """;

        var texto = Texto(
            $"Hola {solicitud.NombreSolicitante},",
            $"Recibimos tu solicitud de acceso a FacturAR para {solicitud.RazonSocial}.",
            "La revisión es manual. Si tu solicitud es aprobada, el próximo contacto llegará por email.",
            "No compartas este mensaje: no contiene credenciales de acceso.");

        return new ContenidoEmail(
            NombreSolicitudRecibida,
            "Recibimos tu solicitud de acceso a FacturAR",
            Layout("Onboarding", "Recibimos tu solicitud", contenido),
            texto);
    }

    public static ContenidoEmail CrearNuevaSolicitud(SolicitudAltaEmpresa solicitud, string portalBaseUrl)
    {
        var panel = $"{portalBaseUrl.TrimEnd('/')}/admin/solicitudes";
        var detalle = $"""
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="margin:0 0 24px;border-collapse:collapse;">
              {Dato("Razón social", Codificar(solicitud.RazonSocial))}
              {Dato("CUIT", $"<span style=\"font-family:'Courier New',monospace;\">{Codificar(solicitud.Cuit)}</span>")}
              {Dato("Email del solicitante", Codificar(solicitud.EmailSolicitante))}
              {Dato("Fecha de recepción", Codificar(Fecha(solicitud.CreatedAt)))}
            </table>
            {Boton(panel, "Revisar solicitudes")}
            <p style="margin:0;color:{ColorTextoSuave};font-size:13px;">
              La solicitud todavía no crea una cuenta: la empresa nace cuando se aprueba y se activa la invitación.
            </p>
            """;

        var contenido = $"""
            <p style="margin:0 0 16px;">Se recibió una nueva solicitud de alta de empresa.</p>
            {detalle}
            """;

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
            Layout("Administración", "Nueva solicitud de alta", contenido),
            texto);
    }

    public static ContenidoEmail CrearSolicitudAprobada(
        SolicitudAltaEmpresa solicitud,
        string linkActivacion,
        DateTime expiraAt)
    {
        var contenido = $"""
            <p style="margin:0 0 16px;">Hola {Codificar(solicitud.NombreSolicitante)},</p>
            <p style="margin:0 0 16px;">
              Tu solicitud de acceso a <strong>FacturAR</strong> para
              <strong>{Codificar(solicitud.RazonSocial)}</strong> fue aprobada.
            </p>
            <p style="margin:0 0 8px;">Ya podés definir tu acceso y empezar a operar.</p>
            {Boton(linkActivacion, "Activar mi cuenta")}
            {Aviso($"El link vence el {Codificar(Fecha(expiraAt))}{Codificar(Vigencia(expiraAt))} y es de un solo uso.", ColorPrimario)}
            <p style="margin:0;color:{ColorTextoSuave};font-size:13px;">
              Si no solicitaste el alta, podés ignorar este mensaje.
            </p>
            """;

        var texto = Texto(
            $"Hola {solicitud.NombreSolicitante},",
            $"Tu solicitud de acceso a FacturAR para {solicitud.RazonSocial} fue aprobada.",
            "Activá tu cuenta desde el siguiente link:",
            linkActivacion,
            $"El link vence el {Fecha(expiraAt)}{Vigencia(expiraAt)} y es de un solo uso.");

        return new ContenidoEmail(
            NombreSolicitudAprobada,
            "Tu solicitud fue aprobada: activá tu cuenta",
            Layout("Onboarding", "Tu solicitud fue aprobada", contenido),
            texto);
    }

    public static ContenidoEmail CrearSolicitudRechazada(SolicitudAltaEmpresa solicitud)
    {
        var motivo = solicitud.MotivoRechazo ?? string.Empty;
        var contenido = $"""
            <p style="margin:0 0 16px;">Hola {Codificar(solicitud.NombreSolicitante)},</p>
            <p style="margin:0 0 16px;">
              Tu solicitud de acceso a <strong>FacturAR</strong> para
              <strong>{Codificar(solicitud.RazonSocial)}</strong> fue rechazada.
            </p>
            {Aviso($"Motivo: {Codificar(motivo)}", ColorPeligro)}
            <p style="margin:0;color:{ColorTextoSuave};font-size:13px;">
              Si creés que se trata de un error, respondé este mensaje para que podamos revisarlo.
            </p>
            """;

        var texto = Texto(
            $"Hola {solicitud.NombreSolicitante},",
            $"Tu solicitud de acceso a FacturAR para {solicitud.RazonSocial} fue rechazada.",
            $"Motivo: {motivo}");

        return new ContenidoEmail(
            NombreSolicitudRechazada,
            "Tu solicitud de acceso fue rechazada",
            Layout("Onboarding", "Tu solicitud fue rechazada", contenido),
            texto);
    }

    public static ContenidoEmail CrearInvitacionReenviada(
        SolicitudAltaEmpresa solicitud,
        string linkActivacion,
        DateTime expiraAt)
    {
        var contenido = $"""
            <p style="margin:0 0 16px;">Hola {Codificar(solicitud.NombreSolicitante)},</p>
            <p style="margin:0 0 16px;">
              Reenviamos tu invitación de acceso a <strong>FacturAR</strong> para
              <strong>{Codificar(solicitud.RazonSocial)}</strong>.
            </p>
            <p style="margin:0 0 8px;">Usá este link para definir tu acceso.</p>
            {Boton(linkActivacion, "Activar mi cuenta")}
            {Aviso($"El link vence el {Codificar(Fecha(expiraAt))}{Codificar(Vigencia(expiraAt))} y es de un solo uso. El link anterior queda sin efecto.", ColorPrimario)}
            <p style="margin:0;color:{ColorTextoSuave};font-size:13px;">
              Si no solicitaste el reenvío, podés ignorar este mensaje.
            </p>
            """;

        var texto = Texto(
            $"Hola {solicitud.NombreSolicitante},",
            $"Reenviamos tu invitación de acceso a FacturAR para {solicitud.RazonSocial}.",
            "Activá tu cuenta desde el siguiente link:",
            linkActivacion,
            $"El link vence el {Fecha(expiraAt)}{Vigencia(expiraAt)} y es de un solo uso. El link anterior queda sin efecto.");

        return new ContenidoEmail(
            NombreInvitacionReenviada,
            "Reenviamos tu invitación de acceso a FacturAR",
            Layout("Onboarding", "Reenviamos tu invitación", contenido),
            texto);
    }

    private static string Layout(string categoria, string titulo, string contenidoHtml) => $"""
        <!DOCTYPE html>
        <html lang="es">
        <head>
        <meta charset="utf-8" />
        <meta name="viewport" content="width=device-width, initial-scale=1" />
        <meta name="color-scheme" content="light" />
        <title>{Codificar(titulo)}</title>
        </head>
        <body style="margin:0;padding:0;background-color:{ColorFondo};color:{ColorTexto};">
        <div style="display:none;max-height:0;max-width:0;overflow:hidden;opacity:0;">{Codificar(titulo)}</div>
        <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="background-color:{ColorFondo};">
        <tr>
        <td align="center" style="padding:32px 16px;">
          <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="max-width:600px;background-color:{ColorSuperficie};border:1px solid {ColorBorde};border-radius:12px;overflow:hidden;font-family:{FuenteBase};">
            <tr><td style="height:6px;background-color:{ColorPrimario};font-size:0;line-height:0;">&nbsp;</td></tr>
            <tr>
              <td style="padding:24px 32px 0;">
                <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0">
                  <tr>
                    <td style="font-family:{FuenteTitulo};font-size:21px;font-weight:bold;color:{ColorTexto};letter-spacing:0.3px;">FacturAR</td>
                    <td align="right" style="font-family:{FuenteBase};font-size:10px;letter-spacing:1.6px;text-transform:uppercase;color:{ColorAcento};">{Codificar(categoria)}</td>
                  </tr>
                </table>
                <div style="height:1px;background-color:#ECEAE2;margin-top:18px;"></div>
              </td>
            </tr>
            <tr>
              <td style="padding:24px 32px 6px;">
                <h1 style="margin:0;font-family:{FuenteTitulo};font-size:25px;line-height:1.3;color:{ColorTexto};">{Codificar(titulo)}</h1>
              </td>
            </tr>
            <tr>
              <td style="padding:10px 32px 32px;font-family:{FuenteBase};font-size:15px;line-height:1.65;color:{ColorTexto};">
                {contenidoHtml}
              </td>
            </tr>
            <tr>
              <td style="padding:20px 32px;background-color:{ColorTexto};">
                <p style="margin:0 0 6px;font-family:{FuenteTitulo};font-size:15px;color:#E9F1F2;">FacturAR</p>
                <p style="margin:0;font-family:{FuenteBase};font-size:11px;line-height:1.6;color:#A7BBC1;">
                  Simulador de facturación electrónica argentina. El CAE es simulado y no tiene validez fiscal.
                </p>
              </td>
            </tr>
            <tr>
              <td style="padding:16px 32px;background-color:#ECEAE2;">
                <p style="margin:0;font-family:{FuenteBase};font-size:11px;line-height:1.6;color:{ColorTextoSuave};">
                  Este mensaje se envió automáticamente. Si no reconocés esta solicitud, podés ignorarlo.
                </p>
              </td>
            </tr>
          </table>
          <p style="max-width:600px;margin:16px auto 0;font-family:{FuenteBase};font-size:11px;color:{ColorTextoSuave};text-align:center;">
            © {DateTime.UtcNow.Year} FacturAR · Proyecto demostrativo
          </p>
        </td>
        </tr>
        </table>
        </body>
        </html>
        """;

    private static string Boton(string url, string texto) => $"""
        <table role="presentation" cellpadding="0" cellspacing="0" border="0" style="margin:8px 0 22px;">
          <tr>
            <td style="background-color:{ColorPrimario};border-radius:6px;">
              <a href="{Codificar(url)}" style="display:inline-block;padding:12px 22px;font-family:{FuenteBase};font-size:14px;font-weight:bold;color:#FFFFFF;text-decoration:none;border-radius:6px;">
                {Codificar(texto)}
              </a>
            </td>
          </tr>
        </table>
        """;

    private static string Aviso(string texto, string color) => $"""
        <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="margin:0 0 20px;background-color:{ColorFondo};border-left:4px solid {color};border-radius:4px;">
          <tr>
            <td style="padding:14px 16px;font-family:{FuenteBase};font-size:13px;line-height:1.6;color:{ColorTextoSuave};">
              {texto}
            </td>
          </tr>
        </table>
        """;

    private static string Dato(string etiqueta, string valorHtml) => $"""
        <tr>
          <td style="padding:9px 0;border-bottom:1px solid #ECEAE2;font-family:{FuenteBase};font-size:11px;letter-spacing:0.7px;text-transform:uppercase;color:{ColorTextoSuave};width:38%;vertical-align:top;">
            {Codificar(etiqueta)}
          </td>
          <td style="padding:9px 0;border-bottom:1px solid #ECEAE2;font-family:{FuenteBase};font-size:14px;color:{ColorTexto};vertical-align:top;">
            {valorHtml}
          </td>
        </tr>
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
