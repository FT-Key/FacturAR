namespace FacturAR.Application.Notificaciones;

public sealed record EmailMessage(
    string Para,
    string Asunto,
    string CuerpoHtml,
    string CuerpoTexto,
    string Plantilla);
