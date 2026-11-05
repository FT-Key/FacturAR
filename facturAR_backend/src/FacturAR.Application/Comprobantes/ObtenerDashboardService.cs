using System.Globalization;
using FacturAR.Application.Abstractions;
using FacturAR.Contracts.Comprobantes;
using FacturAR.Domain.Enums;
using FacturAR.Domain.Exceptions;

namespace FacturAR.Application.Comprobantes;

public sealed class ObtenerDashboardService(
    IComprobanteRepository comprobantes,
    ListarComprobantesService listado,
    ISecuenciaRepository secuencias,
    TimeProvider timeProvider)
{
    private const int CantidadUltimosComprobantes = 5;

    public async Task<DashboardResponse> ObtenerAsync(
        Guid contribuyenteId,
        string? mes,
        CancellationToken ct = default)
    {
        var periodo = ResolverPeriodo(mes);

        var metricas = await comprobantes.ObtenerMetricasAsync(
            contribuyenteId,
            periodo.Desde,
            periodo.Hasta,
            ct);

        var ultimoEmitido = await listado.ListarAsync(
            contribuyenteId,
            page: 1,
            pageSize: 1,
            desde: null,
            hasta: null,
            tipo: null,
            estado: (int)EstadoComprobante.Emitido,
            puntoVenta: null,
            receptor: null,
            ct);

        var ultimos = await listado.ListarAsync(
            contribuyenteId,
            page: 1,
            pageSize: CantidadUltimosComprobantes,
            desde: null,
            hasta: null,
            tipo: null,
            estado: null,
            puntoVenta: null,
            receptor: null,
            ct);

        var ultimoNumeroAutorizado = await secuencias.ObtenerMaximoUltimoNumeroGlobalAsync(
            contribuyenteId,
            ct);

        var ultimoCae = ultimoEmitido.Items.FirstOrDefault()?.Cae;

        return new DashboardResponse(
            periodo,
            metricas.CantidadComprobantes,
            metricas.ImporteTotal,
            string.IsNullOrWhiteSpace(ultimoCae) ? null : ultimoCae,
            ultimoNumeroAutorizado,
            ultimos.Items);
    }

    private PeriodoDashboard ResolverPeriodo(string? mes)
    {
        int anio;
        int numeroMes;

        if (string.IsNullOrWhiteSpace(mes))
        {
            var ahora = timeProvider.GetUtcNow();
            anio = ahora.Year;
            numeroMes = ahora.Month;
        }
        else
        {
            if (!DateTime.TryParseExact(
                    mes.Trim(),
                    "yyyy-MM",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var parseado))
            {
                throw new ComprobanteValidationException(
                    "El parámetro mes debe tener el formato yyyy-MM.");
            }

            anio = parseado.Year;
            numeroMes = parseado.Month;
        }

        var desde = new DateTime(anio, numeroMes, 1, 0, 0, 0, DateTimeKind.Utc);
        return new PeriodoDashboard(desde, desde.AddMonths(1));
    }
}
