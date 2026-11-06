using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FacturAR.Contracts.Comprobantes;

namespace FacturAR.Application.Comprobantes;

public static class PayloadHasher
{
    private static readonly JsonSerializerOptions Opciones = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static string Hash(EmitirComprobanteRequest request)
    {
        var canonico = ConstruirCanonico(request);
        var json = JsonSerializer.Serialize(canonico, Opciones);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));

        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static PayloadCanonico ConstruirCanonico(EmitirComprobanteRequest request)
    {
        var receptor = request.Receptor;
        var items = request.Items ?? [];

        return new PayloadCanonico(
            request.TipoComprobante,
            request.PuntoVenta,
            FormatearFecha(request.FechaEmision),
            new ReceptorCanonico(
                NormalizarTipoDocumento(receptor?.DocumentoTipo),
                NormalizarTexto(receptor?.DocumentoNumero),
                NormalizarTexto(receptor?.RazonSocial),
                receptor?.CondicionIva),
            [.. items.Select(CrearItemCanonico)],
            request.ImporteTotal is decimal importeTotal ? NormalizarDecimal(importeTotal) : null,
            request.Observaciones is null ? null : request.Observaciones.Trim(),
            request.ComprobanteOriginalId);
    }

    private static ItemCanonico? CrearItemCanonico(ItemComprobanteRequest? item)
        => item is null
            ? null
            : new ItemCanonico(
                NormalizarTexto(item.Descripcion),
                NormalizarDecimal(item.Cantidad),
                NormalizarDecimal(item.PrecioUnitario),
                NormalizarDecimal(item.AlicuotaIva));

    private static string? FormatearFecha(DateTime? fecha)
    {
        if (fecha is not DateTime valor)
        {
            return null;
        }

        var utc = valor.Kind == DateTimeKind.Local
            ? valor.ToUniversalTime()
            : DateTime.SpecifyKind(valor, DateTimeKind.Utc);

        return utc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
    }

    private static string? NormalizarTipoDocumento(string? valor)
    {
        var normalizado = NormalizarTexto(valor);

        return string.IsNullOrEmpty(normalizado) ? null : normalizado;
    }

    private static string? NormalizarTexto(string? valor)
        => valor?.Trim();

    private static decimal NormalizarDecimal(decimal valor)
    {
        var redondeado = Math.Round(valor, 2, MidpointRounding.AwayFromZero);

        return decimal.Parse(
            redondeado.ToString("0.##", CultureInfo.InvariantCulture),
            NumberStyles.Number,
            CultureInfo.InvariantCulture);
    }

    private sealed record PayloadCanonico(
        [property: JsonPropertyOrder(0)] int TipoComprobante,
        [property: JsonPropertyOrder(1)] int PuntoVenta,
        [property: JsonPropertyOrder(2)] string? FechaEmision,
        [property: JsonPropertyOrder(3)] ReceptorCanonico Receptor,
        [property: JsonPropertyOrder(4)] IReadOnlyList<ItemCanonico?> Items,
        [property: JsonPropertyOrder(5)] decimal? ImporteTotal,
        [property: JsonPropertyOrder(6)] string? Observaciones,
        [property: JsonPropertyOrder(7)] Guid? ComprobanteOriginalId);

    private sealed record ReceptorCanonico(
        [property: JsonPropertyOrder(0)] string? DocumentoTipo,
        [property: JsonPropertyOrder(1)] string? DocumentoNumero,
        [property: JsonPropertyOrder(2)] string? RazonSocial,
        [property: JsonPropertyOrder(3)] int? CondicionIva);

    private sealed record ItemCanonico(
        [property: JsonPropertyOrder(0)] string? Descripcion,
        [property: JsonPropertyOrder(1)] decimal Cantidad,
        [property: JsonPropertyOrder(2)] decimal PrecioUnitario,
        [property: JsonPropertyOrder(3)] decimal AlicuotaIva);
}
