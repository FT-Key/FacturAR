using System.Text.Json;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FacturAR.Web.Pages;

public sealed class LandingModel : PageModel
{
    private const string Descripcion =
        "Simulador de facturación electrónica argentina. Emití facturas A, B y C, consultá comprobantes y verificá un CAE por API REST o SOAP.";

    private readonly string _baseUrl;

    public LandingModel(IConfiguration configuration)
    {
        _baseUrl = (configuration["Portal:BaseUrl"] ?? "http://localhost:5116").TrimEnd('/');
        JsonLd = ConstruirJsonLd();
    }

    public string Titulo => "FacturAR — Facturación electrónica argentina simulada";

    public string Description => Descripcion;

    public string Canonical => $"{_baseUrl}/";

    public string OgImage => $"{_baseUrl}/brand/og-image.png";

    public string JsonLd { get; }

    public IReadOnlyList<PreguntaFrecuente> PreguntasFrecuentes { get; } =
    [
        new(
            "¿Necesito una empresa para usar FacturAR?",
            "Sí. Para usar la API tenés que registrar tu empresa y esperar la aprobación. No alcanza con registrarte."),
        new(
            "¿Puedo registrarme solo?",
            "No hay registro libre. Primero enviás la solicitud, FacturAR la revisa y, si se aprueba, te llega una invitación por email."),
        new(
            "¿Cuánto tarda la aprobación?",
            "No hay un plazo garantizado. Cuando la solicitud se aprueba, la invitación por email tiene una validez de 48 horas."),
        new(
            "¿La API key es gratis?",
            "Sí, se genera desde el portal sin cargo. Se muestra una sola vez: guardala en un lugar seguro."),
        new(
            "¿El CAE es válido?",
            "No. FacturAR es un simulador: el CAE es simulado y no tiene validez fiscal."),
        new(
            "¿Dónde está la documentación?",
            "La guía de integración online estará disponible próximamente. Mientras tanto, esta página resume REST, SOAP, autenticación e idempotencia.")
    ];

    public sealed record PreguntaFrecuente(string Pregunta, string Respuesta);

    private string ConstruirJsonLd()
    {
        var organizacion = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "Organization",
            ["name"] = "FacturAR",
            ["url"] = Canonical,
            ["logo"] = OgImage
        };

        var sitio = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "WebSite",
            ["name"] = "FacturAR",
            ["url"] = Canonical,
            ["inLanguage"] = "es-AR"
        };

        var preguntas = PreguntasFrecuentes
            .Select(pregunta => new Dictionary<string, object?>
            {
                ["@type"] = "Question",
                ["name"] = pregunta.Pregunta,
                ["acceptedAnswer"] = new Dictionary<string, object?>
                {
                    ["@type"] = "Answer",
                    ["text"] = pregunta.Respuesta
                }
            })
            .ToArray();

        var faq = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@type"] = "FAQPage",
            ["mainEntity"] = preguntas
        };

        return JsonSerializer.Serialize(new object[] { organizacion, sitio, faq });
    }
}
