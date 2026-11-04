using System.Xml.Serialization;

namespace FacturAR.Contracts.Soap;

[XmlType(Namespace = FecaeNamespaces.Fev1)]
public sealed class FECAEAuth
{
    public string? Token { get; set; }

    public string? Sign { get; set; }
}

[XmlType(Namespace = FecaeNamespaces.Fev1)]
public sealed class FecaeItem
{
    public string? Descripcion { get; set; }

    public decimal Cantidad { get; set; }

    public decimal PrecioUnitario { get; set; }

    public decimal AlicuotaIVA { get; set; }
}

[XmlType(Namespace = FecaeNamespaces.Fev1)]
public sealed class FECAESolicitarRequest
{
    public FECAEAuth? Auth { get; set; }

    public string? Cuit { get; set; }

    public int PtoVta { get; set; }

    public int TipoComprobante { get; set; }

    public int Concepto { get; set; }

    public int DocTipo { get; set; }

    public long DocNro { get; set; }

    public int CondicionIVA { get; set; }

    [XmlArray("Items")]
    [XmlArrayItem("Item")]
    public List<FecaeItem>? Items { get; set; }

    public string? IdempotencyKey { get; set; }
}

[XmlType(Namespace = FecaeNamespaces.Fev1)]
public sealed class FECAEGenerarRequest
{
    public FECAEAuth? Auth { get; set; }

    public string? Cuit { get; set; }

    public int PtoVta { get; set; }

    public int TipoComprobante { get; set; }

    public int Concepto { get; set; }

    public int DocTipo { get; set; }

    public long DocNro { get; set; }

    public int CondicionIVA { get; set; }

    [XmlArray("Items")]
    [XmlArrayItem("Item")]
    public List<FecaeItem>? Items { get; set; }

    public string? IdempotencyKey { get; set; }
}
