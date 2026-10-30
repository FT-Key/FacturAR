namespace FacturAR.Contracts.Solicitudes;

public sealed record EditarSolicitudRequest(
    string RazonSocial,
    int CondicionIva,
    string Email,
    string Telefono,
    string Domicilio);

public sealed record RechazarSolicitudRequest(string Motivo);

public sealed record AprobarSolicitudResponse(
    Guid ContribuyenteId,
    Guid InvitacionId,
    bool EmailEnviado,
    string? AdvertenciaEmail);

public sealed record RechazarSolicitudResponse(
    string Estado,
    bool EmailEnviado,
    string? AdvertenciaEmail);

public sealed record ReenviarInvitacionResponse(
    Guid InvitacionId,
    bool EmailEnviado,
    string? AdvertenciaEmail);

public sealed record SolicitudAltaResumenResponse(
    Guid Id,
    string Estado,
    string Cuit,
    string RazonSocial,
    string Email,
    DateTime CreatedAt);

public sealed record SolicitudAltaDetalleResponse(
    Guid Id,
    string Estado,
    string Cuit,
    string RazonSocial,
    int CondicionIva,
    string NombreSolicitante,
    string Email,
    string Telefono,
    string Domicilio,
    string Ciudad,
    string Provincia,
    string CodigoPostal,
    string? Mensaje,
    string? MotivoRechazo,
    Guid? ContribuyenteId,
    Guid? RevisadaPorAdministradorId,
    DateTime? RevisadaAt,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    InvitacionResumenResponse? Invitacion,
    IReadOnlyList<EventoRevisionResponse> Historial);

public sealed record InvitacionResumenResponse(
    Guid Id,
    string Estado,
    string Email,
    string TokenPrefix,
    DateTime ExpiraAt,
    DateTime? UltimoEnvioAt,
    int IntentosEnvio,
    bool PuedeReenviarse);

public sealed record EventoRevisionResponse(
    string Accion,
    string ActorTipo,
    Guid? AdministradorId,
    DateTime CreatedAt);
