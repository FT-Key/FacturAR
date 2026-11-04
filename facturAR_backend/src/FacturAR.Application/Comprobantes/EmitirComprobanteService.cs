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
    private const int ReintentosAjusteSaldo = 5;
    private const string MensajeSaldoCambiante =
        "El saldo del comprobante original cambió durante la emisión. Intentá de nuevo.";

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
        var emisor = await ObtenerEmisorActivoAsync(contribuyenteId, ct);

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

        var preparada = await PrepararAsync(contribuyenteId, rol, request, emisor, ct);

        Comprobante comprobante;

        try
        {
            comprobante = await _transaccion.EjecutarAsync(
                async token =>
                {
                    if (preparada.EsNota)
                    {
                        await AjustarSaldoOriginalAsync(
                            contribuyenteId,
                            preparada.ComprobanteOriginalId!.Value,
                            preparada.Tipo,
                            preparada.Calculo.ImporteTotal,
                            token);
                    }

                    var numero = await _secuencias.ObtenerSiguienteNumeroAsync(
                        contribuyenteId,
                        request.PuntoVenta,
                        preparada.Tipo,
                        token);

                    var ahora = DateTime.UtcNow;

                    var emitido = new Comprobante
                    {
                        Id = Guid.NewGuid(),
                        ContribuyenteId = contribuyenteId,
                        EmisorCuit = preparada.EmisorCuit,
                        EmisorRazonSocial = preparada.EmisorRazonSocial,
                        PuntoVenta = request.PuntoVenta,
                        TipoComprobante = preparada.Tipo,
                        Numero = numero,
                        FechaEmision = preparada.FechaEmision,
                        Cae = CaeGenerator.Generar(),
                        CaeVencimiento = preparada.CaeVencimiento,
                        ReceptorDocumentoTipo = preparada.Receptor.DocumentoTipo,
                        ReceptorDocumentoNumero = preparada.Receptor.DocumentoNumero,
                        ReceptorRazonSocial = preparada.Receptor.RazonSocial,
                        ReceptorCondicionIva = preparada.Receptor.CondicionIva,
                        ImporteNeto = preparada.Calculo.ImporteNeto,
                        ImporteIva = preparada.Calculo.ImporteIva,
                        ImporteTotal = preparada.Calculo.ImporteTotal,
                        Moneda = preparada.Moneda,
                        Estado = EstadoComprobante.Emitido,
                        IdempotencyKey = idempotencyKey,
                        IdempotencyPayloadHash = payloadHash,
                        Observaciones = preparada.Observaciones,
                        Items = [.. preparada.Calculo.Items],
                        ComprobanteOriginalId = preparada.EsNota ? preparada.ComprobanteOriginalId : null,
                        ImporteOriginalAjustado = preparada.EsNota ? null : preparada.Calculo.ImporteTotal,
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

    public Task ValidarAsync(
        Guid contribuyenteId,
        string? rol,
        EmitirComprobanteRequest request,
        CancellationToken ct = default)
        => PrepararAsync(contribuyenteId, rol, request, null, ct);

    private async Task<EmisionPreparada> PrepararAsync(
        Guid contribuyenteId,
        string? rol,
        EmitirComprobanteRequest request,
        Contribuyente? emisor,
        CancellationToken ct)
    {
        ValidarPermisos(rol);

        emisor ??= await ObtenerEmisorActivoAsync(contribuyenteId, ct);

        var tipo = (TipoComprobante)request.TipoComprobante;
        ComprobanteValidator.ValidarTipoHabilitado(tipo);
        NotaComprobanteValidator.ValidarReferencia(tipo, request.ComprobanteOriginalId);
        ComprobanteValidator.ValidarPuntoVenta(request.PuntoVenta);
        var esNota = NotaComprobanteValidator.EsNota(tipo);

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

        if (esNota)
        {
            var original = await ObtenerOriginalAsync(contribuyenteId, request.ComprobanteOriginalId!.Value, ct);
            NotaComprobanteValidator.ValidarOriginal(tipo, original);
            NotaComprobanteValidator.CalcularNuevoSaldo(
                tipo,
                NotaComprobanteValidator.SaldoActual(original),
                calculo.ImporteTotal);
        }

        var fechaEmision = ResolverFechaEmision(request.FechaEmision);
        var caeVencimiento = CalcularCaeVencimiento(fechaEmision, diasVencimiento);
        var moneda = await _parametros.ObtenerMonedaAsync(contribuyenteId, ct);

        return new EmisionPreparada(
            tipo,
            emisor.Cuit,
            emisor.RazonSocial,
            receptor,
            calculo,
            fechaEmision,
            caeVencimiento,
            moneda,
            observaciones,
            esNota,
            request.ComprobanteOriginalId);
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

    private async Task<Comprobante> ObtenerOriginalAsync(Guid contribuyenteId, Guid originalId, CancellationToken ct)
        => await _comprobantes.GetByIdAsync(contribuyenteId, originalId, ct)
            ?? throw new ComprobanteNoEncontradoException(NotaComprobanteValidator.MensajeOriginalNoEncontrado);

    private async Task AjustarSaldoOriginalAsync(
        Guid contribuyenteId,
        Guid originalId,
        TipoComprobante tipoNota,
        decimal importeNota,
        CancellationToken ct)
    {
        for (var intento = 0; intento < ReintentosAjusteSaldo; intento++)
        {
            var original = await ObtenerOriginalAsync(contribuyenteId, originalId, ct);
            NotaComprobanteValidator.ValidarOriginal(tipoNota, original);

            var saldoNuevo = NotaComprobanteValidator.CalcularNuevoSaldo(
                tipoNota,
                NotaComprobanteValidator.SaldoActual(original),
                importeNota);

            if (await _comprobantes.AjustarSaldoAsync(
                    contribuyenteId,
                    originalId,
                    original.ImporteOriginalAjustado,
                    saldoNuevo,
                    ct))
            {
                return;
            }
        }

        throw new ComprobanteValidationException(MensajeSaldoCambiante);
    }

    private async Task<Contribuyente> ObtenerEmisorActivoAsync(Guid contribuyenteId, CancellationToken ct)
    {
        var emisor = await _contribuyentes.GetByIdAsync(contribuyenteId, ct);
        if (emisor is null || !emisor.Activo)
        {
            throw new CuentaInactivaException();
        }

        return emisor;
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
                ["importeTotal"] = comprobante.ImporteTotal.ToString(CultureInfo.InvariantCulture),
                ["comprobanteOriginalId"] = comprobante.ComprobanteOriginalId?.ToString() ?? string.Empty
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

    private sealed record EmisionPreparada(
        TipoComprobante Tipo,
        string EmisorCuit,
        string EmisorRazonSocial,
        ReceptorValidado Receptor,
        CalculoComprobante Calculo,
        DateTime FechaEmision,
        DateTime CaeVencimiento,
        string Moneda,
        string? Observaciones,
        bool EsNota,
        Guid? ComprobanteOriginalId);
}
