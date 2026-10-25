using FacturAR.Domain.Enums;

namespace FacturAR.Domain.Entities;

public sealed class Secuencia
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ContribuyenteId { get; set; }
    public int PuntoVenta { get; set; }
    public TipoComprobante TipoComprobante { get; set; }
    public long UltimoNumero { get; set; }
}
