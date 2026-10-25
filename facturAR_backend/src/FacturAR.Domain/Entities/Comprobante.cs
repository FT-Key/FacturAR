using FacturAR.Domain.Enums;

namespace FacturAR.Domain.Entities;

public sealed class Comprobante
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ContribuyenteId { get; set; }
    public string EmisorCuit { get; set; } = string.Empty;
    public string EmisorRazonSocial { get; set; } = string.Empty;
    public int PuntoVenta { get; set; }
    public TipoComprobante TipoComprobante { get; set; }
    public long Numero { get; set; }
    public DateTime FechaEmision { get; set; } = DateTime.UtcNow;
    public string Cae { get; set; } = string.Empty;
    public DateTime CaeVencimiento { get; set; }
    public string ReceptorDocumentoTipo { get; set; } = "99";
    public string ReceptorDocumentoNumero { get; set; } = string.Empty;
    public string ReceptorRazonSocial { get; set; } = string.Empty;
    public CondicionIva ReceptorCondicionIva { get; set; } = CondicionIva.ConsumidorFinal;
    public decimal ImporteNeto { get; set; }
    public decimal ImporteIva { get; set; }
    public decimal ImporteTotal { get; set; }
    public string Moneda { get; set; } = "ARS";
    public EstadoComprobante Estado { get; set; } = EstadoComprobante.Emitido;
    public string IdempotencyKey { get; set; } = Guid.NewGuid().ToString("N");
    public string? Observaciones { get; set; }
    public List<ComprobanteItem> Items { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
