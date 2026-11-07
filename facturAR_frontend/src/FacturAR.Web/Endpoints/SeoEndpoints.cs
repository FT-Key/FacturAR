using System.Text;

namespace FacturAR.Web.Endpoints;

public static class SeoEndpoints
{
    private static readonly string[] RutasPublicas = ["/", "/solicitar-alta", "/verificar", "/manual"];

    private static readonly string[] RutasPrivadas =
    [
        "/dashboard",
        "/comprobantes",
        "/api-keys",
        "/admin",
        "/login",
        "/activar",
        "/logout"
    ];

    public static IEndpointRouteBuilder MapSeoEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/robots.txt", (IConfiguration configuration) =>
        {
            var baseUrl = ObtenerBaseUrl(configuration);
            var contenido = new StringBuilder();
            contenido.AppendLine("User-agent: *");
            foreach (var ruta in RutasPublicas)
            {
                contenido.Append("Allow: ").AppendLine(ruta);
            }

            foreach (var ruta in RutasPrivadas)
            {
                contenido.Append("Disallow: ").AppendLine(ruta);
            }

            contenido.AppendLine();
            contenido.Append("Sitemap: ").Append(baseUrl).AppendLine("/sitemap.xml");

            return Results.Text(contenido.ToString(), "text/plain; charset=utf-8");
        });

        app.MapGet("/sitemap.xml", (IConfiguration configuration) =>
        {
            var baseUrl = ObtenerBaseUrl(configuration);
            var contenido = new StringBuilder();
            contenido.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            contenido.AppendLine("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">");
            foreach (var ruta in RutasPublicas)
            {
                contenido.Append("  <url><loc>")
                    .Append(System.Net.WebUtility.HtmlEncode($"{baseUrl}{ruta}"))
                    .AppendLine("</loc></url>");
            }

            contenido.AppendLine("</urlset>");

            return Results.Text(contenido.ToString(), "application/xml; charset=utf-8");
        });

        return app;
    }

    private static string ObtenerBaseUrl(IConfiguration configuration)
        => (configuration["Portal:BaseUrl"] ?? "http://localhost:5116").TrimEnd('/');
}
