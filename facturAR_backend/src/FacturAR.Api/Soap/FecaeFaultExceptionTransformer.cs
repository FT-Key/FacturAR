using System.Net;
using System.Reflection;
using System.ServiceModel.Channels;
using System.Xml;
using MongoDB.Driver;
using SoapCore;
using SoapCore.Extensibility;

namespace FacturAR.Api.Soap;

public sealed class FecaeFaultExceptionTransformer : IFaultExceptionTransformer
{
    private const string Soap11EnvelopeNamespace = "http://schemas.xmlsoap.org/soap/envelope/";
    private const string PrefijoSoapPredeterminado = "s";
    private const string MensajeSobreInvalido = "El sobre SOAP no es válido.";
    private const string MensajeMongoNoDisponible = "No hay conexión con la base de datos. Intentá de nuevo en unos minutos.";
    private const string MensajeErrorInterno = "Ocurrió un error inesperado.";

    private readonly ILogger<FecaeFaultExceptionTransformer> _logger;

    public FecaeFaultExceptionTransformer(ILogger<FecaeFaultExceptionTransformer> logger)
        => _logger = logger;

    public Message ProvideFault(
        Exception exception,
        MessageVersion messageVersion,
        Message requestMessage,
        ConcurrentXmlNamespaceLookup xmlNamespaceLookup)
    {
        var error = Mapear(exception);
        var prefix = xmlNamespaceLookup.LookupPrefix(Soap11EnvelopeNamespace) ?? PrefijoSoapPredeterminado;
        var faultCode = error.StatusCode >= StatusCodes.Status500InternalServerError ? "Server" : "Client";
        var bodyWriter = new FecaeFaultBodyWriter(error.Codigo, error.Mensaje, faultCode, prefix);
        var faultMessage = Message.CreateMessage(messageVersion, null, bodyWriter);

        faultMessage.Properties.Add(
            HttpResponseMessageProperty.Name,
            new HttpResponseMessageProperty { StatusCode = (HttpStatusCode)error.StatusCode });

        return faultMessage;
    }

    private (int StatusCode, string Codigo, string Mensaje) Mapear(Exception exception)
    {
        var ex = exception is TargetInvocationException { InnerException: not null } target
            ? target.InnerException
            : exception;

        switch (ex)
        {
            case FecaeFaultException fault:
                return (fault.StatusCode, fault.Codigo, fault.Mensaje);
            case XmlException or FormatException:
                return (StatusCodes.Status400BadRequest, "VALIDACION", MensajeSobreInvalido);
            case MongoConnectionException or TimeoutException:
                return (StatusCodes.Status503ServiceUnavailable, "MONGO_NO_DISPONIBLE", MensajeMongoNoDisponible);
            default:
                _logger.LogError(ex, "Error inesperado al procesar una solicitud SOAP.");
                return (StatusCodes.Status500InternalServerError, "ERROR_INTERNO", MensajeErrorInterno);
        }
    }
}
