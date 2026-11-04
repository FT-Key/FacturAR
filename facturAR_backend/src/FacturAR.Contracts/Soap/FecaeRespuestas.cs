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
