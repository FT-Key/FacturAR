using FacturAR.Application.Abstractions;
using FacturAR.Application.Comprobantes;
using FacturAR.Contracts.Comprobantes;

namespace FacturAR.Api.Comprobantes;

public static class DashboardEndpoint
{
    public static IEndpointRouteBuilder MapComprobanteDashboardEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/v1/comprobantes/dashboard", ObtenerAsync)
            .WithName("ObtenerDashboardComprobantes")
            .WithTags("Comprobantes")
            .WithSummary("Devuelve las métricas del mes y los últimos comprobantes del contribuyente.")
            .WithDescription(
                "El parámetro mes es opcional y usa el formato yyyy-MM; sin él se toma el mes actual en UTC. "
                + "La cantidad y el importe total cuentan solo comprobantes emitidos dentro del periodo.")
            .RequireAuthorization(ComprobanteConsultaEndpoints.Politica)
            .Produces<DashboardResponse>()
            .ConErroresComunes();

        return app;
    }

    private static Task<IResult> ObtenerAsync(
        string? mes,
        HttpContext httpContext,
        ITenantContext tenant,
        ObtenerDashboardService service,
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
            async () => Results.Ok(await service.ObtenerAsync(
                contribuyenteId,
                mes,
                cancellationToken)));
    }
}
