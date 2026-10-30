using FacturAR.Application.Abstractions;
using FacturAR.Contracts.Comunes;
using FacturAR.Contracts.Solicitudes;
using FacturAR.Domain.Enums;
using FacturAR.Domain.Exceptions;
using FacturAR.Domain.Validation;

namespace FacturAR.Application.Solicitudes;

public sealed class ListarSolicitudesService
{
    public const int PageSizePorDefecto = 20;
    public const int PageSizeMaximo = 100;

    private const int FiltroMaximo = 200;

    private readonly ISolicitudAltaRepository _solicitudes;

    public ListarSolicitudesService(ISolicitudAltaRepository solicitudes)
    {
        _solicitudes = solicitudes;
    }

    public async Task<PaginaResponse<SolicitudAltaResumenResponse>> ListarAsync(
        int? page,
        int? pageSize,
        string? estado,
        string? cuit,
        string? razonSocial,
        string? email,
        CancellationToken ct = default)
    {
        var pagina = page ?? 1;
        var tamanio = pageSize ?? PageSizePorDefecto;
        if (pagina < 1 || tamanio is < 1 or > PageSizeMaximo)
        {
            throw new SolicitudAltaValidationException($"page debe ser mayor o igual a 1 y pageSize estar entre 1 y {PageSizeMaximo}.");
        }

        EstadoSolicitud? estadoFiltro = null;
        if (!string.IsNullOrWhiteSpace(estado))
        {
            if (!Enum.TryParse<EstadoSolicitud>(estado.Trim(), ignoreCase: true, out var valor)
                || !Enum.IsDefined(valor)
                || int.TryParse(estado, out _))
            {
                throw new SolicitudAltaValidationException("El estado indicado no es válido.");
            }

            estadoFiltro = valor;
        }

        var cuitFiltro = string.IsNullOrWhiteSpace(cuit) ? null : CuitValidator.Normalize(cuit);
        var razonSocialFiltro = Texto(razonSocial);
        var emailFiltro = Texto(email)?.ToLowerInvariant();

        var (items, total) = await _solicitudes.ListarAsync(
            new FiltroSolicitudes(estadoFiltro, cuitFiltro, razonSocialFiltro, emailFiltro, pagina, tamanio),
            ct);

        return new PaginaResponse<SolicitudAltaResumenResponse>(
            items
                .Select(s => new SolicitudAltaResumenResponse(
                    s.Id,
                    s.Estado.ToString(),
                    s.Cuit,
                    s.RazonSocial,
                    s.EmailSolicitante,
                    s.CreatedAt))
                .ToList(),
            pagina,
            tamanio,
            total,
            (int)Math.Ceiling(total / (double)tamanio));
    }

    private static string? Texto(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        var normalizado = valor.Trim();
        if (normalizado.Length > FiltroMaximo)
        {
            throw new SolicitudAltaValidationException("El filtro es demasiado largo.");
        }

        return normalizado;
    }
}
