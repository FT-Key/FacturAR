using FacturAR.Web.Services;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace FacturAR.Web.Endpoints;

public static class ManualEndpoints
{
    public const string RutaDescargaPostman = "/downloads/FacturAR.postman_collection.json";

    public static IEndpointRouteBuilder MapManualEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(
            RutaDescargaPostman,
            (
                HttpContext context,
                IOptions<ManualOptions> options,
                IWebHostEnvironment environment,
                ILoggerFactory loggerFactory) =>
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

                context.Response.GetTypedHeaders().CacheControl = new CacheControlHeaderValue
                {
                    Public = true,
                    MaxAge = TimeSpan.FromHours(1)
                };

                return Results.File(
                    rutaFisica,
                    "application/json",
                    "FacturAR.postman_collection.json");
            }).AllowAnonymous();

        return app;
    }
}

