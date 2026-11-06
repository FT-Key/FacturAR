namespace FacturAR.Contracts.PuntosVenta;

public sealed record PuntoVentaResumenResponse(Guid Id, int Numero, string Descripcion, bool Activo);
