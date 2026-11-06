using FacturAR.Application.Abstractions;
using FacturAR.Contracts.Comprobantes;
using FacturAR.Domain.Entities;
using FacturAR.Domain.Enums;
using FacturAR.Domain.Exceptions;
using FacturAR.Domain.Validation;

namespace FacturAR.Application.Comprobantes;

public sealed class CalcularPresupuestoService
{
    private readonly IParametrosResolver _parametros;

    public CalcularPresupuestoService(IParametrosResolver parametros)
    {
        _parametros = parametros;
    }

    public async Task<PresupuestoResponse> CalcularAsync(
        Guid contribuyenteId,
        EmitirComprobanteRequest request,
        CancellationToken ct = default)
    {
        var tipo = (TipoComprobante)request.TipoComprobante;
        ComprobanteValidator.ValidarTipoHabilitado(tipo);
        ComprobanteValidator.ValidarPuntoVenta(request.PuntoVenta);

        var alicuotasPermitidas = await _parametros.ObtenerAlicuotasPermitidasAsync(contribuyenteId, ct);

        ComprobanteValidator.ValidarReceptor(
            tipo,
            request.Receptor?.DocumentoTipo,
            request.Receptor?.DocumentoNumero,
            request.Receptor?.RazonSocial,
            request.Receptor?.CondicionIva);

        var itemsSolicitados = request.Items ?? [];
        if (itemsSolicitados.Any(item => item is null))
        {
            throw new ComprobanteValidationException("Los ítems del comprobante no son válidos.");
        }

        var items = itemsSolicitados.Select(CrearItem).ToList();
        ComprobanteValidator.ValidarItems(items, alicuotasPermitidas);

        var observaciones = string.IsNullOrWhiteSpace(request.Observaciones) ? null : request.Observaciones.Trim();
        ComprobanteValidator.ValidarObservaciones(observaciones);

        var calculo = CalculadoraImportes.Calcular(items, tipo);
        ComprobanteValidator.ValidarImporteTotal(request.ImporteTotal, calculo.ImporteTotal);

        return new PresupuestoResponse(
            calculo.ImporteNeto,
            calculo.ImporteIva,
            calculo.ImporteTotal,
            [.. calculo.Items.Select(item => new PresupuestoItemResponse(
                item.Descripcion,
                item.Cantidad,
                item.PrecioUnitario,
                item.AlicuotaIva,
                item.Subtotal))]);
    }

    private static ComprobanteItem CrearItem(ItemComprobanteRequest item)
        => new()
        {
            Descripcion = (item.Descripcion ?? string.Empty).Trim(),
            Cantidad = item.Cantidad,
            PrecioUnitario = item.PrecioUnitario,
            AlicuotaIva = item.AlicuotaIva
        };
}
