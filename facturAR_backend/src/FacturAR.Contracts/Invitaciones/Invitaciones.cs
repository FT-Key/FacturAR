namespace FacturAR.Contracts.Invitaciones;

public sealed record InvitacionValidacionResponse(
    string RazonSocial,
    string EmailEnmascarado,
    DateTime ExpiraAt);

public sealed record AceptarInvitacionRequest(
    string Password,
    string ConfirmacionPassword);

public sealed record AceptarInvitacionResponse(string Mensaje);
