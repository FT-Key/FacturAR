using System.ServiceModel.Channels;
using System.Xml;

namespace FacturAR.Api.Soap;

public sealed class FecaeFaultBodyWriter : BodyWriter
{
    private const string Soap11EnvelopeNamespace = "http://schemas.xmlsoap.org/soap/envelope/";

    private readonly string _codigo;
    private readonly string _mensaje;
    private readonly string _faultCode;
    private readonly string _prefix;

    public FecaeFaultBodyWriter(string codigo, string mensaje, string faultCode, string prefix)
        : base(isBuffered: true)
    {
        _codigo = codigo;
        _mensaje = mensaje;
        _faultCode = faultCode;
        _prefix = prefix;
    }

    protected override void OnWriteBodyContents(XmlDictionaryWriter writer)
    {
        writer.WriteStartElement("Fault", Soap11EnvelopeNamespace);
        writer.WriteElementString("faultcode", $"{_prefix}:{_faultCode}");
        writer.WriteElementString("faultstring", $"{_codigo}: {_mensaje}");

        writer.WriteStartElement("detail");
        writer.WriteElementString("codigo", _codigo);
        writer.WriteElementString("mensaje", _mensaje);
        writer.WriteEndElement();

        writer.WriteEndElement();
    }
}
