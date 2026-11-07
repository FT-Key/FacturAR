using FacturAR.Web.Services;
using Microsoft.Extensions.Options;

namespace FacturAR.Web.Endpoints;

public static class ManualEndpoints
{
    public const string RutaDescargaPostman = "/downloads/FacturAR.postman_collection.json";

    public static IEndpointRouteBuilder MapManualEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(
            RutaDescargaPostman,
            async (
                HttpContext context,
                IOptions<ManualOptions> options,
                IWebHostEnvironment environment,
                ILoggerFactory loggerFactory,
                CancellationToken cancellationToken) =>
            {
                var opciones = options.Value;
                var rutaFisica = ManualOptions.ResolverRutaFisica(
                    opciones.RutaColeccionPostman,
                    ManualOptions.DefaultRutaColeccionPostman,
                    Path.Combine("docs", "postman", "FacturAR.postman_collection.json"),
                    environment.ContentRootPath);

                if (rutaFisica is null)
                {
                    var logger = loggerFactory.CreateLogger("FacturAR.Web.ManualEndpoints");
                    logger.LogError(
                        "No se encontró la colección de Postman configurada en '{RutaColeccionPostman}'.",
                        opciones.RutaColeccionPostman);
                    return Results.NotFound();
                }

                var contenido = await File.ReadAllTextAsync(rutaFisica, cancellationToken);
                context.Response.Headers.ContentDisposition = "attachment; filename=\"FacturAR.postman_collection.json\"";
                return Results.Text(contenido, "application/json; charset=utf-8");
            }).AllowAnonymous();

        return app;
    }
}

