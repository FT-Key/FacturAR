using System.Globalization;
using System.Text.Json;
using FacturAR.Application.Abstractions;
using FacturAR.Contracts.Comprobantes;
using FacturAR.Domain.Entities;
using FacturAR.Domain.Enums;
using FacturAR.Domain.Exceptions;
using FacturAR.Domain.Validation;

namespace FacturAR.Application.Comprobantes;

public sealed class EmitirComprobanteService : IEmitirComprobanteService
{
    public const string AccionEmitido = "comprobante.emitido";

    private const string EntidadComprobante = "Comprobante";
    private const string ActorEmpresa = "Empresa";
    private const string RolAdministrador = "Administrador";
    private const string RolEmisor = "Emisor";

    private readonly IContribuyenteRepository _contribuyentes;
    private readonly IPuntoVentaRepository _puntosVenta;
    private readonly ISecuenciaService _secuencias;
    private readonly IComprobanteRepository _comprobantes;
    private readonly IIdempotenciaService _idempotencia;
    private readonly IEventoAuditoriaRepository _eventos;
    private readonly ITransaccionMongo _transaccion;
    private readonly IParametrosResolver _parametros;

    public EmitirComprobanteService(
        IContribuyenteRepository contribuyentes,
        IPuntoVentaRepository puntosVenta,
        ISecuenciaService secuencias,
        IComprobanteRepository comprobantes,
        IIdempotenciaService idempotencia,
        IEventoAuditoriaRepository eventos,
        ITransaccionMongo transaccion,
        IParametrosResolver parametros)
    {
        _contribuyentes = contribuyentes;
        _puntosVenta = puntosVenta;
        _secuencias = secuencias;
        _comprobantes = comprobantes;
        _idempotencia = idempotencia;
        _eventos = eventos;
        _transaccion = transaccion;
        _parametros = parametros;
    }

    public async Task<ResultadoEmision> EmitirAsync(
        Guid contribuyenteId,
        string? rol,
        EmitirComprobanteRequest request,
        string idempotencyKey,
        CancellationToken ct = default)
    {
        ValidarPermisos(rol);

        var emisor = await _contribuyentes.GetByIdAsync(contribuyenteId, ct);
        if (emisor is null || !emisor.Activo)
        {
            throw new CuentaInactivaException();
        }

        var payloadHash = PayloadHasher.Hash(request);
        var resolucion = await _idempotencia.ResolverAsync(contribuyenteId, idempotencyKey, payloadHash, ct);

        if (resolucion.Estado == EstadoIdempotencia.Replay && resolucion.Comprobante is not null)
        {
            return new ResultadoEmision(Mapear(resolucion.Comprobante), true);
        }

        if (resolucion.Estado == EstadoIdempotencia.Conflicto)
        {
            throw new IdempotenciaConflictoException();
        }

        var tipo = (TipoComprobante)request.TipoComprobante;
        ComprobanteValidator.ValidarTipoHabilitado(tipo);
        ComprobanteValidator.ValidarPuntoVenta(request.PuntoVenta);

        var puntoVenta = await _puntosVenta.ObtenerPorNumeroAsync(contribuyenteId, request.PuntoVenta, ct);
        if (puntoVenta is null)
        {
            throw new ComprobanteNoEncontradoException(
                "El punto de venta no existe o no pertenece al contribuyente autenticado.");
        }

        if (!puntoVenta.Activo)
        {
            throw new PuntoVentaInactivoException();
        }

        var alicuotasPermitidas = await _parametros.ObtenerAlicuotasPermitidasAsync(contribuyenteId, ct);
        var diasVencimiento = await _parametros.ObtenerDiasVencimientoCaeAsync(contribuyenteId, ct);

        ComprobanteValidator.ValidarEmisor(emisor, tipo);

        var receptor = ComprobanteValidator.ValidarReceptor(
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

        var fechaEmision = ResolverFechaEmision(request.FechaEmision);
        var caeVencimiento = CalcularCaeVencimiento(fechaEmision, diasVencimiento);
        var moneda = await _parametros.ObtenerMonedaAsync(contribuyenteId, ct);

        Comprobante comprobante;

        try
        {
            comprobante = await _transaccion.EjecutarAsync(
                async token =>
                {
                    var numero = await _secuencias.ObtenerSiguienteNumeroAsync(
                        contribuyenteId,
                        request.PuntoVenta,
                        tipo,
                        token);

                    var ahora = DateTime.UtcNow;

                    var emitido = new Comprobante
                    {
                        Id = Guid.NewGuid(),
                        ContribuyenteId = contribuyenteId,
                        EmisorCuit = emisor.Cuit,
                        EmisorRazonSocial = emisor.RazonSocial,
                        PuntoVenta = request.PuntoVenta,
                        TipoComprobante = tipo,
                        Numero = numero,
                        FechaEmision = fechaEmision,
                        Cae = CaeGenerator.Generar(),
                        CaeVencimiento = caeVencimiento,
                        ReceptorDocumentoTipo = receptor.DocumentoTipo,
                        ReceptorDocumentoNumero = receptor.DocumentoNumero,
                        ReceptorRazonSocial = receptor.RazonSocial,
                        ReceptorCondicionIva = receptor.CondicionIva,
                        ImporteNeto = calculo.ImporteNeto,
                        ImporteIva = calculo.ImporteIva,
                        ImporteTotal = calculo.ImporteTotal,
                        Moneda = moneda,
                        Estado = EstadoComprobante.Emitido,
                        IdempotencyKey = idempotencyKey,
                        IdempotencyPayloadHash = payloadHash,
                        Observaciones = observaciones,
                        Items = [.. calculo.Items],
                        CreatedAt = ahora
                    };

                    await _comprobantes.AddAsync(emitido, token);
                    await _eventos.AddAsync(CrearEvento(emitido), token);

                    return emitido;
                },
                ct);
        }
        catch (IdempotenciaDuplicadaException)
        {
            var existente = await ReleerTrasCarreraAsync(contribuyenteId, idempotencyKey, ct);

            if (existente is null)
            {
                throw new IdempotenciaConflictoException();
            }

            if (!string.Equals(existente.IdempotencyPayloadHash, payloadHash, StringComparison.Ordinal))
            {
                throw new IdempotenciaConflictoException();
            }

            return new ResultadoEmision(Mapear(existente), true);
        }

        return new ResultadoEmision(Mapear(comprobante), false);
    }

    private async Task<Comprobante?> ReleerTrasCarreraAsync(
        Guid contribuyenteId,
        string idempotencyKey,
        CancellationToken ct)
    {
        const int intentos = 10;
        var espera = TimeSpan.FromMilliseconds(25);

        for (var intento = 0; intento < intentos; intento++)
        {
            var existente = await _comprobantes.GetByIdempotencyKeyAsync(contribuyenteId, idempotencyKey, ct);

            if (existente is not null)
            {
                return existente;
            }

            if (intento < intentos - 1)
            {
                await Task.Delay(espera, ct);
            }
        }

        return null;
    }

    private static void ValidarPermisos(string? rol)
    {
        if (string.IsNullOrEmpty(rol)
            || string.Equals(rol, RolAdministrador, StringComparison.Ordinal)
            || string.Equals(rol, RolEmisor, StringComparison.Ordinal))
        {
            return;
        }

        throw new SinPermisosException("El rol del principal no permite emitir comprobantes.");
    }

    private static ComprobanteItem CrearItem(ItemComprobanteRequest item)
        => new()
        {
            Descripcion = (item.Descripcion ?? string.Empty).Trim(),
            Cantidad = item.Cantidad,
            PrecioUnitario = item.PrecioUnitario,
            AlicuotaIva = item.AlicuotaIva
        };

    private static DateTime ResolverFechaEmision(DateTime? fechaEmision)
    {
        if (fechaEmision is not DateTime fecha)
        {
            return DateTime.UtcNow;
        }

        return fecha.Kind == DateTimeKind.Local
            ? fecha.ToUniversalTime()
            : DateTime.SpecifyKind(fecha, DateTimeKind.Utc);
    }

    private static DateTime CalcularCaeVencimiento(DateTime fechaEmision, int diasVencimiento)
    {
        try
        {
            return CaeGenerator.CalcularVencimiento(fechaEmision, diasVencimiento);
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new ComprobanteValidationException("La fecha de emisión no es válida.");
        }
    }

    private static EventoAuditoria CrearEvento(Comprobante comprobante)
        => new()
        {
            Accion = AccionEmitido,
            Entidad = EntidadComprobante,
            EntidadId = comprobante.Id.ToString(),
            ContribuyenteId = comprobante.ContribuyenteId,
            UsuarioId = null,
            ActorTipo = ActorEmpresa,
            Datos = JsonSerializer.Serialize(new Dictionary<string, string>
            {
                ["numero"] = comprobante.Numero.ToString(CultureInfo.InvariantCulture),
                ["cae"] = comprobante.Cae,
                ["tipoComprobante"] = ((int)comprobante.TipoComprobante).ToString(CultureInfo.InvariantCulture),
                ["puntoVenta"] = comprobante.PuntoVenta.ToString(CultureInfo.InvariantCulture),
                ["importeTotal"] = comprobante.ImporteTotal.ToString(CultureInfo.InvariantCulture)
            }),
            CreatedAt = comprobante.CreatedAt
        };

    private static ComprobanteResponse Mapear(Comprobante comprobante)
        => new(
            comprobante.Id,
            (int)comprobante.TipoComprobante,
            comprobante.TipoComprobante.ObtenerDescripcion(),
            comprobante.PuntoVenta,
            comprobante.Numero,
            comprobante.FechaEmision,
            comprobante.Cae,
            comprobante.CaeVencimiento,
            new ReceptorComprobanteResponse(
                comprobante.ReceptorDocumentoTipo,
                comprobante.ReceptorDocumentoNumero,
                comprobante.ReceptorRazonSocial,
                (int)comprobante.ReceptorCondicionIva),
            comprobante.ImporteNeto,
            comprobante.ImporteIva,
            comprobante.ImporteTotal,
            comprobante.Moneda,
            comprobante.Estado.ToString());
}
