namespace FacturAR.Web.Models;

public sealed class EmisionModelo
{
    public int TipoComprobante { get; set; } = 11;

    public int PuntoVenta { get; set; }

    public ReceptorEmisionModelo Receptor { get; set; } = new();

    public List<EmisionItemModelo> Items { get; set; } = [new()];

    public string ClaveIdempotencia { get; set; } = string.Empty;

    public string FirmaPayload { get; set; } = string.Empty;

    public string? Observaciones { get; set; }
}

public sealed class ReceptorEmisionModelo
{
    public string DocumentoTipo { get; set; } = "99";

    public string DocumentoNumero { get; set; } = "0";

    public string RazonSocial { get; set; } = "Consumidor Final";

    public int CondicionIva { get; set; } = 5;
}

public sealed class EmisionItemModelo
{
    public string Descripcion { get; set; } = "Servicio";

    public decimal Cantidad { get; set; } = 1m;

    public decimal PrecioUnitario { get; set; }

    public decimal AlicuotaIva { get; set; }
}
