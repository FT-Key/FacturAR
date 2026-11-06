using FacturAR.Application.Abstractions;
using FacturAR.Application.Comprobantes;
using FacturAR.Contracts.Comunes;

namespace FacturAR.Api.Comprobantes;

public static class ComprobantePdfEndpoint
{
    public static IEndpointRouteBuilder MapComprobantePdfEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/v1/comprobantes/{id:guid}/pdf", ObtenerPdfAsync)
            .WithName("DescargarComprobantePdf")
            .WithTags("Comprobantes")
            .WithSummary("Devuelve el PDF de un comprobante propio.")
            .WithDescription(
                "Responde application/pdf con Content-Disposition inline y el nombre "
                + "{puntoVenta:D4}-{numero:D8}.pdf. Incluye los datos fiscales, los ítems, los importes "
                + "y un código QR que apunta a la verificación pública por CAE.")
            .RequireAuthorization(ComprobanteConsultaEndpoints.Politica)
            .Produces(StatusCodes.Status200OK, contentType: "application/pdf")
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
            .ConErroresComunes();

        app.MapGet("/v1/comprobantes/{id}/pdf", IdentificadorInvalido)
            .ExcludeFromDescription()
            .RequireAuthorization(ComprobanteConsultaEndpoints.Politica)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest);

        return app;
    }

    private static Task<IResult> ObtenerPdfAsync(
        Guid id,
        HttpContext httpContext,
        ITenantContext tenant,
        ConsultarComprobanteService consultas,
        IPdfComprobanteService pdf,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        if (ComprobanteConsultaEndpoints.ValidarAcceso(httpContext, tenant, out var contribuyenteId) is IResult error)
        {
            return Task.FromResult(error);
        }

        return ComprobanteConsultaEndpoints.EjecutarAsync(
            loggerFactory.CreateLogger(ComprobanteConsultaEndpoints.CategoriaLogger),
            contribuyenteId,
            async () =>
            {
                var comprobante = await consultas.ConsultarEntidadAsync(contribuyenteId, id, cancellationToken);

                if (comprobante is null)
                {
                    return ComprobanteConsultaEndpoints.Error(
                        StatusCodes.Status404NotFound,
                        "COMPROBANTE_NO_ENCONTRADO",
                        "No existe un comprobante con el id indicado.");
                }

                var bytes = await pdf.GenerarAsync(comprobante, cancellationToken);

                if (bytes.Length == 0)
                {
                    throw new InvalidOperationException("El PDF generado no contiene datos.");
                }

                httpContext.Response.Headers.ContentDisposition =
                    $"inline; filename=\"{ComprobantePdfNombres.NombreArchivo(comprobante)}\"";

                return Results.File(bytes, "application/pdf", enableRangeProcessing: false);
            });
    }

    private static IResult IdentificadorInvalido()
        => ComprobanteConsultaEndpoints.Error(
            StatusCodes.Status400BadRequest,
            "VALIDACION",
            "El identificador del comprobante no es válido.");
}
