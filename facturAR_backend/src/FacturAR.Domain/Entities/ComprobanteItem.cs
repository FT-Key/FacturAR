namespace FacturAR.Domain.Entities;

public sealed class ComprobanteItem
{
    public string Descripcion { get; set; } = string.Empty;
    public decimal Cantidad { get; set; }
    public decimal PrecioUnitario { get; set; }
    public decimal AlicuotaIva { get; set; }
    public decimal Subtotal { get; set; }
}
