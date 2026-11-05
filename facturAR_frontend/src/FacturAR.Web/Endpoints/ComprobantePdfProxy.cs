using System.Net;
using FacturAR.Web.Services;

namespace FacturAR.Web.Endpoints;

public static class ComprobantePdfProxy
{
    public static IEndpointRouteBuilder MapComprobantePdfProxy(this IEndpointRouteBuilder app)
    {
        app.MapGet("/comprobantes/{id:guid}/pdf", async (
            Guid id,
            HttpContext httpContext,
            FacturarApiClient apiClient,
            IConfiguration configuration,
            CancellationToken cancellationToken) =>
        {
            var nombreCookie = configuration["Auth:Cookie:NombreContribuyente"]
                ?? ".FacturAR.Contribuyente";

            if (!httpContext.Request.Cookies.TryGetValue(nombreCookie, out var valor)
                || string.IsNullOrWhiteSpace(valor))
            {
                return Results.Redirect("/login");
            }

            var cookie = $"{nombreCookie}={valor}";

            var detalle = await apiClient.ObtenerComprobanteAsync(cookie, id, cancellationToken);

            if (detalle.SinSesion)
            {
                return Results.Redirect("/login");
            }

            if (detalle.Status == HttpStatusCode.NotFound)
            {
                return Results.NotFound();
            }

            if (detalle.Status == HttpStatusCode.Forbidden)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (!detalle.Exitoso || detalle.Valor is null)
            {
                return Results.StatusCode(EstadoError(detalle.Status));
            }

            var pdf = await apiClient.DescargarPdfAsync(cookie, id, cancellationToken);

            if (pdf.SinSesion)
            {
                return Results.Redirect("/login");
            }

            if (pdf.Status == HttpStatusCode.NotFound)
            {
                return Results.NotFound();
            }

            if (!pdf.Exitoso || pdf.Valor is null)
            {
                return Results.StatusCode(EstadoError(pdf.Status));
            }

            var nombreArchivo = $"{detalle.Valor.PuntoVenta:D4}-{detalle.Valor.Numero:D8}.pdf";
            httpContext.Response.Headers.ContentDisposition = $"inline; filename=\"{nombreArchivo}\"";

            return Results.File(pdf.Valor, "application/pdf");
        });

        return app;
    }

    private static int EstadoError(HttpStatusCode status)
        => status == HttpStatusCode.ServiceUnavailable
            ? StatusCodes.Status503ServiceUnavailable
            : StatusCodes.Status500InternalServerError;
}
