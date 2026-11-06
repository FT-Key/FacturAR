using System.Xml.Serialization;

namespace FacturAR.Contracts.Soap;

[XmlType(Namespace = FecaeNamespaces.Fev1)]
public sealed class FeCabResp
{
    public string? Resultado { get; set; }

    public string? CodAutorizacion { get; set; }

    public string? FchVto { get; set; }

    public string? Observaciones { get; set; }
}

[XmlType(Namespace = FecaeNamespaces.Fev1)]
public sealed class FECAESolicitarResponse
{
    public FeCabResp? FeCabResp { get; set; }
}

[XmlType(Namespace = FecaeNamespaces.Fev1)]
public sealed class Respuesta
{
    public string? Resultado { get; set; }

    public string? Error { get; set; }

    public string? Observaciones { get; set; }
}

[XmlType(Namespace = FecaeNamespaces.Fev1)]
public sealed class FECAEGenerarResponse
{
    public Respuesta? Respuesta { get; set; }
}

[XmlType(Namespace = FecaeNamespaces.Fev1)]
public sealed class FECompUltimoAutorizadoResponse
{
    public string? Resultado { get; set; }

    public int PtoVta { get; set; }

    public int TipoComprobante { get; set; }

    public long UltimoNro { get; set; }

    public string? Cae { get; set; }

    public string? FechaEmision { get; set; }

    public string? FchVto { get; set; }

    public string? Observaciones { get; set; }

    public bool ShouldSerializePtoVta()
        => string.Equals(Resultado, "A", StringComparison.Ordinal);

    public bool ShouldSerializeTipoComprobante()
        => string.Equals(Resultado, "A", StringComparison.Ordinal);

    public bool ShouldSerializeUltimoNro()
        => string.Equals(Resultado, "A", StringComparison.Ordinal);
}

[XmlType(Namespace = FecaeNamespaces.Fev1)]
public sealed class FECompConsultarResponse
{
    public string? Resultado { get; set; }

    public string? Cuit { get; set; }

    public int PtoVta { get; set; }

    public int TipoComprobante { get; set; }

    public long Numero { get; set; }

    public string? FechaEmision { get; set; }

    public string? Cae { get; set; }

    public string? FchVto { get; set; }

    public string? DocTipo { get; set; }

    public string? DocNro { get; set; }

    public int CondicionIVA { get; set; }

    public string? Moneda { get; set; }

    public decimal ImporteNeto { get; set; }

    public decimal ImporteIva { get; set; }

    public decimal ImporteTotal { get; set; }

    public string? Estado { get; set; }

    public string? Observaciones { get; set; }

    public bool ShouldSerializePtoVta()
        => string.Equals(Resultado, "A", StringComparison.Ordinal);

    public bool ShouldSerializeTipoComprobante()
        => string.Equals(Resultado, "A", StringComparison.Ordinal);

    public bool ShouldSerializeNumero()
        => string.Equals(Resultado, "A", StringComparison.Ordinal);

    public bool ShouldSerializeCondicionIVA()
        => string.Equals(Resultado, "A", StringComparison.Ordinal);

    public bool ShouldSerializeImporteNeto()
        => string.Equals(Resultado, "A", StringComparison.Ordinal);

    public bool ShouldSerializeImporteIva()
        => string.Equals(Resultado, "A", StringComparison.Ordinal);

    public bool ShouldSerializeImporteTotal()
        => string.Equals(Resultado, "A", StringComparison.Ordinal);
}

[XmlType(Namespace = FecaeNamespaces.Fev1)]
public sealed class FEDummyResponse
{
    public string? Estado { get; set; }

    public string? ServerTime { get; set; }

    public string? Version { get; set; }

    public string? Service { get; set; }
}
