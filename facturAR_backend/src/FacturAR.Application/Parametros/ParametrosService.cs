using System.Globalization;
using FacturAR.Application.Abstractions;

namespace FacturAR.Application.Parametros;

public sealed class ParametrosService : IParametrosResolver
{
    private const char SeparadorLista = ';';

    private readonly IParametrosRepository _repository;
    private readonly ParametrosFacturacion _parametros;

    public ParametrosService(IParametrosRepository repository, ParametrosFacturacion parametros)
    {
        _repository = repository;
        _parametros = parametros;
    }

    public async Task<string?> ObtenerAsync(
        string clave,
        Guid? contribuyenteId,
        CancellationToken ct = default)
    {
        if (contribuyenteId is { } id && id != Guid.Empty)
        {
            var overrideValor = await _repository.ObtenerValorAsync(id, clave, ct);
            if (!string.IsNullOrWhiteSpace(overrideValor))
            {
                return overrideValor;
            }
        }

        return _parametros.ObtenerValor(clave);
    }

    public async Task<int> ObtenerDiasVencimientoCaeAsync(
        Guid? contribuyenteId,
        CancellationToken ct = default)
        => await ObtenerEnteroAsync(
            nameof(ParametrosFacturacion.DiasVencimientoCae),
            ParametrosFacturacion.DiasVencimientoCaePredeterminado,
            contribuyenteId,
            ct);

    public async Task<IReadOnlyList<decimal>> ObtenerAlicuotasPermitidasAsync(
        Guid? contribuyenteId,
        CancellationToken ct = default)
    {
        var valor = await ObtenerAsync(nameof(ParametrosFacturacion.AlicuotasPermitidas), contribuyenteId, ct);
        return ParsearAlicuotas(valor) ?? ParametrosFacturacion.AlicuotasPredeterminadas;
    }

    public async Task<int> ObtenerPuntoVentaPorDefectoAsync(
        Guid? contribuyenteId,
        CancellationToken ct = default)
        => await ObtenerEnteroAsync(
            nameof(ParametrosFacturacion.PuntoVentaPorDefecto),
            ParametrosFacturacion.PuntoVentaPredeterminado,
            contribuyenteId,
            ct);

    public async Task<string> ObtenerMonedaAsync(Guid? contribuyenteId, CancellationToken ct = default)
    {
        var valor = await ObtenerAsync(nameof(ParametrosFacturacion.Moneda), contribuyenteId, ct);
        return string.IsNullOrWhiteSpace(valor) ? ParametrosFacturacion.MonedaPredeterminada : valor.Trim();
    }

    public async Task<int> ObtenerPaginaPorDefectoAsync(
        Guid? contribuyenteId,
        CancellationToken ct = default)
        => await ObtenerEnteroAsync(
            nameof(ParametrosFacturacion.PaginaPorDefecto),
            ParametrosFacturacion.PaginaPredeterminada,
            contribuyenteId,
            ct);

    public async Task<int> ObtenerPaginaMaximaAsync(Guid? contribuyenteId, CancellationToken ct = default)
        => await ObtenerEnteroAsync(
            nameof(ParametrosFacturacion.PaginaMaxima),
            ParametrosFacturacion.PaginaMaximaPredeterminada,
            contribuyenteId,
            ct);

    private async Task<int> ObtenerEnteroAsync(
        string clave,
        int predeterminado,
        Guid? contribuyenteId,
        CancellationToken ct)
    {
        var valor = await ObtenerAsync(clave, contribuyenteId, ct);

        return int.TryParse(valor, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numero) && numero > 0
            ? numero
            : predeterminado;
    }

    private static IReadOnlyList<decimal>? ParsearAlicuotas(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        var partes = valor.Split(SeparadorLista, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (partes.Length == 0)
        {
            return null;
        }

        var alicuotas = new List<decimal>(partes.Length);
        foreach (var parte in partes)
        {
            if (!decimal.TryParse(parte, NumberStyles.Number, CultureInfo.InvariantCulture, out var alicuota))
            {
                return null;
            }

            alicuotas.Add(alicuota);
        }

        return alicuotas;
    }
}
