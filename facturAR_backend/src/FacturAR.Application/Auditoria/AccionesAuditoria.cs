namespace FacturAR.Application.Auditoria;

public static class AccionesAuditoria
{
    public const string LoginExitoso = "LoginExitoso";
    public const string LoginFallido = "LoginFallido";
    public const string Logout = "Logout";
    public const string AccesoDenegado = "AccesoDenegado";
    public const string AdminGlobalCreado = "AdminGlobalCreado";
    public const string SolicitudCreada = "SolicitudCreada";
    public const string SolicitudCuitRegistrado = "solicitud.cuit_registrado";
    public const string SolicitudAprobada = "SolicitudAprobada";
    public const string SolicitudRechazada = "SolicitudRechazada";
    public const string SolicitudEditada = "solicitud.editada";
    public const string InvitacionEnviada = "InvitacionEnviada";
    public const string InvitacionReenviada = "InvitacionReenviada";
    public const string CuentaActivada = "CuentaActivada";
    public const string ApiKeyCreada = "ApiKeyCreada";
    public const string ApiKeyRevocada = "ApiKeyRevocada";
    public const string ApiKeyRegenerada = "ApiKeyRegenerada";
    public const string ComprobanteEmitido = "ComprobanteEmitido";
    public const string NotaEmitida = "NotaEmitida";
    public const string OutboxProcesado = "OutboxProcesado";
    public const string OutboxFallido = "OutboxFallido";
    public const string EmailEnviado = "email.enviado";
    public const string EmailFallido = "email.fallido";
}

public static class EntidadesAuditoria
{
    public const string Usuario = "Usuario";
    public const string AdministradorFacturar = "AdministradorFacturar";
    public const string SolicitudAltaEmpresa = "SolicitudAltaEmpresa";
    public const string Invitacion = "Invitacion";
    public const string ApiKey = "ApiKey";
    public const string Comprobante = "Comprobante";
    public const string OutboxMessage = "OutboxMessage";
    public const string Recurso = "Recurso";
}
