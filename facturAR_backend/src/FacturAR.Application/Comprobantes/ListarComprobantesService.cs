using System.Globalization;
using FacturAR.Application.Abstractions;
using FacturAR.Contracts.Comprobantes;
using FacturAR.Contracts.Comunes;
using FacturAR.Domain.Entities;
using FacturAR.Domain.Enums;
using FacturAR.Domain.Exceptions;

namespace FacturAR.Application.Comprobantes;

public sealed class ListarComprobantesService
{
    public const int PageSizePorDefecto = 20;
    public const int PageSizeMaximo = 100;

    private readonly IComprobanteRepository _comprobantes;

    public ListarComprobantesService(IComprobanteRepository comprobantes)
    {
        _comprobantes = comprobantes;
    }

    public async Task<PaginaResponse<ComprobanteResumenResponse>> ListarAsync(
        Guid contribuyenteId,
        int? page,
        int? pageSize,
        string? desde,
        string? hasta,
        int? tipo,
        int? estado,
        int? puntoVenta,
        string? receptor,
        CancellationToken ct = default)
    {
        var pagina = page ?? 1;
        if (pagina < 1)
        {
            throw new ComprobanteValidationException("page debe ser mayor o igual a 1.");
        }

        var tamanio = pageSize ?? PageSizePorDefecto;
        if (tamanio is < 1 or > PageSizeMaximo)
        {
            throw new ComprobanteValidationException(
                $"pageSize debe estar entre 1 y {PageSizeMaximo}.");
        }

        var filtro = CrearFiltro(desde, hasta, tipo, estado, puntoVenta, receptor);

        var (items, total) = await _comprobantes.ListarAsync(
            contribuyenteId,
            filtro,
            pagina,
            tamanio,
            ct);

        return new PaginaResponse<ComprobanteResumenResponse>(
            items.Select(Mapear).ToList(),
            pagina,
            tamanio,
            total,
            (int)Math.Ceiling(total / (double)tamanio));
    }

    private static FiltroListadoComprobantes CrearFiltro(
        string? desde,
        string? hasta,
        int? tipo,
        int? estado,
        int? puntoVenta,
        string? receptor)
    {
        var fechaDesde = ParsearFecha(desde, "desde");
        var fechaHasta = ParsearFecha(hasta, "hasta");

        if (fechaDesde is DateTime inicio && fechaHasta is DateTime fin && inicio.Date > fin.Date)
        {
            throw new ComprobanteValidationException("desde no puede ser posterior a hasta.");
        }

        TipoComprobante? tipoFiltro = null;
        if (tipo is int valorTipo)
        {
            if (!Enum.IsDefined(typeof(TipoComprobante), valorTipo))
            {
                throw new ComprobanteValidationException("El tipo de comprobante indicado no es válido.");
            }

            tipoFiltro = (TipoComprobante)valorTipo;
        }

        EstadoComprobante? estadoFiltro = null;
        if (estado is int valorEstado)
        {
            if (!Enum.IsDefined(typeof(EstadoComprobante), valorEstado))
            {
                throw new ComprobanteValidationException("El estado indicado no es válido.");
            }

            estadoFiltro = (EstadoComprobante)valorEstado;
        }

        if (puntoVenta is int numeroPuntoVenta && numeroPuntoVenta < 1)
        {
            throw new ComprobanteValidationException("puntoVenta debe ser mayor o igual a 1.");
        }

        return new FiltroListadoComprobantes(
            fechaDesde,
            fechaHasta?.Date.AddDays(1),
            tipoFiltro,
            estadoFiltro,
            puntoVenta,
            NormalizarReceptor(receptor));
    }

    private static DateTime? ParsearFecha(string? valor, string nombre)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        if (!DateTime.TryParse(
                valor.Trim(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out var fecha))
        {
            throw new ComprobanteValidationException($"El parámetro {nombre} no es una fecha válida.");
        }

        return fecha.Kind == DateTimeKind.Local
            ? fecha.ToUniversalTime()
            : DateTime.SpecifyKind(fecha, DateTimeKind.Utc);
    }

    private static string? NormalizarReceptor(string? receptor)
    {
        if (string.IsNullOrWhiteSpace(receptor))
        {
            return null;
        }

        var normalizado = new string(
            receptor.Where(caracter => caracter != '-' && !char.IsWhiteSpace(caracter)).ToArray());

        return normalizado.Length == 0 ? null : normalizado;
    }

    private static ComprobanteResumenResponse Mapear(Comprobante comprobante)
        => new(
            comprobante.Id,
            (int)comprobante.TipoComprobante,
            comprobante.TipoComprobante.ObtenerDescripcion(),
            comprobante.PuntoVenta,
            comprobante.Numero,
            comprobante.FechaEmision,
            comprobante.Cae,
            comprobante.ReceptorRazonSocial,
            comprobante.ReceptorDocumentoNumero,
            comprobante.ImporteTotal,
            comprobante.Moneda,
            comprobante.Estado.ToString());
}
