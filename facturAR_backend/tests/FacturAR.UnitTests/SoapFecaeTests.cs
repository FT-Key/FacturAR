using System.Net;
using System.ServiceModel.Channels;
using FacturAR.Api.Auth;
using FacturAR.Api.Soap;
using FacturAR.Application.Abstractions;
using FacturAR.Application.ApiKeys;
using FacturAR.Application.Comprobantes;
using FacturAR.Contracts.Comprobantes;
using FacturAR.Contracts.Soap;
using FacturAR.Domain.Entities;
using FacturAR.Domain.Enums;
using FacturAR.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SoapCore;

namespace FacturAR.UnitTests;

public sealed class SoapFecaeTests
{
    private const string CuitEmisor = "20123456786";
    private const string CuitAjeno = "30712345671";
    private const string ApiKey = "api-key-soap-de-prueba";

    [Fact]
    public async Task FECAESolicitar_Exitosa_DelegaEnEmisionYDevuelveSobreA()
    {
        var contexto = new SoapContexto();

        var respuesta = await contexto.Contrato.FECAESolicitar(contexto.Solicitud());

        Assert.Equal("A", respuesta.FeCabResp?.Resultado);
        Assert.Equal("12345678901234", respuesta.FeCabResp?.CodAutorizacion);
        Assert.Equal("20261113", respuesta.FeCabResp?.FchVto);
        Assert.True(contexto.Emision.EmitirLlamado);
        Assert.Equal(contexto.ContribuyenteId, contexto.Emision.UltimoContribuyenteId);
        Assert.Null(contexto.Emision.UltimoRol);
        Assert.Equal("idem-1", contexto.Emision.UltimoIdempotencyKey);

        var emision = Assert.IsType<EmitirComprobanteRequest>(contexto.Emision.UltimoRequest);
        Assert.Equal(11, emision.TipoComprobante);
        Assert.Equal(1, emision.PuntoVenta);
        Assert.Null(emision.FechaEmision);
        Assert.Null(emision.ImporteTotal);
        Assert.Null(emision.Receptor?.RazonSocial);
        Assert.Equal("99", emision.Receptor?.DocumentoTipo);
        Assert.Equal("0", emision.Receptor?.DocumentoNumero);
        Assert.Equal(5, emision.Receptor?.CondicionIva);
        var item = Assert.Single(emision.Items!);
        Assert.Equal("Servicio", item.Descripcion);
        Assert.Equal(100m, item.PrecioUnitario);
    }

    [Fact]
    public async Task FECAESolicitar_UsaTokenDelSobreSiNoHayHeader()
    {
        var contexto = new SoapContexto();
        contexto.Http.Request.Headers.Clear();

        await contexto.Contrato.FECAESolicitar(contexto.Solicitud());

        Assert.Equal("token-del-sobre", contexto.Autenticador.UltimoValor);
    }

    [Fact]
    public async Task FECAESolicitar_SinCredencial_LanzaFaultApiKeyAusente()
    {
        var contexto = new SoapContexto();
        contexto.Http.Request.Headers.Clear();
        var solicitud = contexto.Solicitud();
        solicitud.Auth = null;

        var excepcion = await Assert.ThrowsAsync<FecaeFaultException>(
            () => contexto.Contrato.FECAESolicitar(solicitud));

        Assert.Equal(StatusCodes.Status401Unauthorized, excepcion.StatusCode);
        Assert.Equal("API_KEY_AUSENTE", excepcion.Codigo);
        Assert.False(contexto.Emision.EmitirLlamado);
    }

    [Fact]
    public async Task FECAESolicitar_ApiKeyInvalida_LanzaFaultApiKeyInvalida()
    {
        var contexto = new SoapContexto();
        contexto.Autenticador.Resultado = null;

        var excepcion = await Assert.ThrowsAsync<FecaeFaultException>(
            () => contexto.Contrato.FECAESolicitar(contexto.Solicitud()));

        Assert.Equal(StatusCodes.Status401Unauthorized, excepcion.StatusCode);
        Assert.Equal("API_KEY_INVALIDA", excepcion.Codigo);
        Assert.DoesNotContain(ApiKey, excepcion.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FECAESolicitar_CuitDeOtraEmpresa_DevuelveRechazoDeAislamiento()
    {
        var contexto = new SoapContexto();

        var respuesta = await contexto.Contrato.FECAESolicitar(contexto.Solicitud(CuitAjeno));

        Assert.Equal("R", respuesta.FeCabResp?.Resultado);
        Assert.StartsWith("VALIDACION: ", respuesta.FeCabResp?.Observaciones, StringComparison.Ordinal);
        Assert.False(contexto.Emision.EmitirLlamado);
        Assert.Equal(1, contexto.Contribuyentes.Consultas);
    }

    [Fact]
    public async Task FECAESolicitar_CuitInvalido_DevuelveRechazoCuitInvalido()
    {
        var contexto = new SoapContexto();

        var respuesta = await contexto.Contrato.FECAESolicitar(contexto.Solicitud("123"));

        Assert.Equal("R", respuesta.FeCabResp?.Resultado);
        Assert.StartsWith("CUIT_INVALIDO: ", respuesta.FeCabResp?.Observaciones, StringComparison.Ordinal);
        Assert.False(contexto.Emision.EmitirLlamado);
    }

    [Fact]
    public async Task FECAESolicitar_ErrorDeNegocio_DevuelveSobreR()
    {
        var contexto = new SoapContexto();
        contexto.Emision.ErrorEmitir = new PuntoVentaInactivoException();

        var respuesta = await contexto.Contrato.FECAESolicitar(contexto.Solicitud());

        Assert.Equal("R", respuesta.FeCabResp?.Resultado);
        Assert.Equal(
            "PUNTO_VENTA_INACTIVO: El punto de venta está inactivo.",
            respuesta.FeCabResp?.Observaciones);
    }

    [Fact]
    public async Task FECAESolicitar_ConflictoDeIdempotencia_DevuelveSobreR()
    {
        var contexto = new SoapContexto();
        contexto.Emision.ErrorEmitir = new IdempotenciaConflictoException();

        var respuesta = await contexto.Contrato.FECAESolicitar(contexto.Solicitud());

        Assert.Equal("R", respuesta.FeCabResp?.Resultado);
        Assert.StartsWith("IDEMPOTENCIA_CONFLICTO: ", respuesta.FeCabResp?.Observaciones, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FECAESolicitar_IdempotencyKeyLarga_DevuelveValidacion()
    {
        var contexto = new SoapContexto();

        var respuesta = await contexto.Contrato.FECAESolicitar(
            contexto.Solicitud(idempotencyKey: new string('x', 129)));

        Assert.Equal("R", respuesta.FeCabResp?.Resultado);
        Assert.StartsWith("VALIDACION: ", respuesta.FeCabResp?.Observaciones, StringComparison.Ordinal);
        Assert.False(contexto.Emision.EmitirLlamado);
    }

    [Fact]
    public async Task FECAESolicitar_SinIdempotencyKey_GeneraClaveSoap()
    {
        var contexto = new SoapContexto();

        await contexto.Contrato.FECAESolicitar(contexto.Solicitud(idempotencyKey: null));

        Assert.StartsWith("soap-", contexto.Emision.UltimoIdempotencyKey, StringComparison.Ordinal);
        Assert.Equal(37, contexto.Emision.UltimoIdempotencyKey!.Length);
    }

    [Fact]
    public async Task FECAEGenerar_Valido_NoEmiteNiPersiste()
    {
        var contexto = new SoapContexto();

        var respuesta = await contexto.Contrato.FECAEGenerar(contexto.SolicitudGenerar());

        Assert.Equal("A", respuesta.Respuesta?.Resultado);
        Assert.True(contexto.Emision.ValidarLlamado);
        Assert.False(contexto.Emision.EmitirLlamado);
        Assert.Null(respuesta.Respuesta?.Error);
    }

    [Fact]
    public async Task FECAEGenerar_ErrorDeNegocio_DevuelveCodigoCanonico()
    {
        var contexto = new SoapContexto();
        contexto.Emision.ErrorValidar = new ComprobanteValidationException("Los ítems no son válidos.");

        var respuesta = await contexto.Contrato.FECAEGenerar(contexto.SolicitudGenerar());

        Assert.Equal("R", respuesta.Respuesta?.Resultado);
        Assert.Equal("VALIDACION", respuesta.Respuesta?.Error);
        Assert.Equal("Los ítems no son válidos.", respuesta.Respuesta?.Observaciones);
        Assert.False(contexto.Emision.EmitirLlamado);
    }

    [Fact]
    public async Task FECAEGenerar_CuitAjeno_DevuelveRechazoDeAislamiento()
    {
        var contexto = new SoapContexto();

        var respuesta = await contexto.Contrato.FECAEGenerar(contexto.SolicitudGenerar(CuitAjeno));

        Assert.Equal("R", respuesta.Respuesta?.Resultado);
        Assert.Equal("VALIDACION", respuesta.Respuesta?.Error);
        Assert.False(contexto.Emision.ValidarLlamado);
        Assert.False(contexto.Emision.EmitirLlamado);
    }

    [Fact]
    public void FecaeMapeador_MapeaDocumentoTipoCuitYConsumidorFinal()
    {
        var solicitud = new FECAESolicitarRequest
        {
            Cuit = CuitEmisor,
            PtoVta = 2,
            TipoComprobante = 1,
            DocTipo = 80,
            DocNro = 30712345671,
            CondicionIVA = 1,
            Items = [new FecaeItem { Descripcion = "Servicio", Cantidad = 2m, PrecioUnitario = 10m, AlicuotaIVA = 21m }]
        };

        var emision = FecaeMapeador.AEmision(solicitud);
        Assert.Equal("CUIT", emision.Receptor?.DocumentoTipo);
        Assert.Equal("30712345671", emision.Receptor?.DocumentoNumero);
        Assert.Equal(1, emision.Receptor?.CondicionIva);
        Assert.Null(emision.Receptor?.RazonSocial);
        Assert.Null(emision.ImporteTotal);

        solicitud.DocTipo = 99;
        solicitud.DocNro = 30123456;
        Assert.Equal("99", FecaeMapeador.AEmision(solicitud).Receptor?.DocumentoTipo);

        solicitud.DocTipo = 1;
        Assert.Equal("1", FecaeMapeador.AEmision(solicitud).Receptor?.DocumentoTipo);
    }

    [Fact]
    public void FecaeMapeador_MapeaRespuestaYRechazo()
    {
        var comprobante = new ComprobanteResponse(
            Guid.NewGuid(),
            11,
            "Factura C",
            1,
            7,
            new DateTime(2026, 11, 3, 0, 0, 0, DateTimeKind.Utc),
            "12345678901234",
            new DateTime(2026, 11, 13, 0, 0, 0, DateTimeKind.Utc),
            new ReceptorComprobanteResponse("99", "0", string.Empty, 5),
            100m,
            0m,
            100m,
            "ARS",
            "Emitido");

        var aprobada = FecaeMapeador.AAprobada(comprobante);
        Assert.Equal("A", aprobada.FeCabResp?.Resultado);
        Assert.Equal("12345678901234", aprobada.FeCabResp?.CodAutorizacion);
        Assert.Equal("20261113", aprobada.FeCabResp?.FchVto);

        var rechazada = FecaeMapeador.ARechazada("VALIDACION", "Mensaje.");
        Assert.Equal("R", rechazada.FeCabResp?.Resultado);
        Assert.Equal("VALIDACION: Mensaje.", rechazada.FeCabResp?.Observaciones);

        var generar = FecaeMapeador.GenerarRechazada("EMISION_INVALIDA", "Mensaje.");
        Assert.Equal("R", generar.Respuesta?.Resultado);
        Assert.Equal("EMISION_INVALIDA", generar.Respuesta?.Error);
        Assert.Equal("Mensaje.", generar.Respuesta?.Observaciones);
    }

    [Fact]
    public void FaultTransformer_Autenticacion_GeneraFaultClienteConDetalle()
    {
        var transformer = new FecaeFaultExceptionTransformer(NullLogger<FecaeFaultExceptionTransformer>.Instance);
        var lookup = new ConcurrentXmlNamespaceLookup();
        lookup.AddNamespace("s", "http://schemas.xmlsoap.org/soap/envelope/");

        var message = transformer.ProvideFault(
            new FecaeFaultException(StatusCodes.Status401Unauthorized, "API_KEY_AUSENTE", "Falta el header X-Api-Key."),
            MessageVersion.Soap11,
            requestMessage: null!,
            lookup);

        var xml = LeerCuerpo(message);
        Assert.Contains("s:Client", xml);
        Assert.Contains("API_KEY_AUSENTE: Falta el header X-Api-Key.", xml);
        Assert.Contains("<codigo>API_KEY_AUSENTE</codigo>", xml);
        Assert.Contains("<mensaje>Falta el header X-Api-Key.</mensaje>", xml);
        Assert.Equal(HttpStatusCode.Unauthorized, ObtenerStatus(message));
    }

    [Fact]
    public void FaultTransformer_MongoNoDisponible_GeneraFaultServidor503()
    {
        var transformer = new FecaeFaultExceptionTransformer(NullLogger<FecaeFaultExceptionTransformer>.Instance);
        var lookup = new ConcurrentXmlNamespaceLookup();
        lookup.AddNamespace("s", "http://schemas.xmlsoap.org/soap/envelope/");

        var message = transformer.ProvideFault(
            new FecaeFaultException(StatusCodes.Status503ServiceUnavailable, "MONGO_NO_DISPONIBLE", "No hay conexión."),
            MessageVersion.Soap11,
            requestMessage: null!,
            lookup);

        Assert.Contains("s:Server", LeerCuerpo(message));
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ObtenerStatus(message));
    }

    [Fact]
    public void FaultTransformer_Timeout_MapeaMongoNoDisponible()
    {
        var transformer = new FecaeFaultExceptionTransformer(NullLogger<FecaeFaultExceptionTransformer>.Instance);
        var lookup = new ConcurrentXmlNamespaceLookup();
        lookup.AddNamespace("s", "http://schemas.xmlsoap.org/soap/envelope/");

        var message = transformer.ProvideFault(
            new TimeoutException("timeout interno"),
            MessageVersion.Soap11,
            requestMessage: null!,
            lookup);

        var xml = LeerCuerpo(message);
        Assert.Contains("MONGO_NO_DISPONIBLE", xml);
        Assert.DoesNotContain("timeout interno", xml, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ObtenerStatus(message));
    }

    [Fact]
    public void FaultTransformer_ErrorDesconocido_NoFiltraDetalleInterno()
    {
        var transformer = new FecaeFaultExceptionTransformer(NullLogger<FecaeFaultExceptionTransformer>.Instance);
        var lookup = new ConcurrentXmlNamespaceLookup();
        lookup.AddNamespace("s", "http://schemas.xmlsoap.org/soap/envelope/");

        var message = transformer.ProvideFault(
            new InvalidOperationException("detalle interno"),
            MessageVersion.Soap11,
            requestMessage: null!,
            lookup);

        var xml = LeerCuerpo(message);
        Assert.Contains("ERROR_INTERNO", xml);
        Assert.DoesNotContain("detalle interno", xml, StringComparison.Ordinal);
        Assert.Contains("s:Server", xml);
        Assert.Equal(HttpStatusCode.InternalServerError, ObtenerStatus(message));
    }

    private static string LeerCuerpo(Message message)
    {
        using var reader = message.GetReaderAtBodyContents();
        return reader.ReadOuterXml();
    }

    private static HttpStatusCode ObtenerStatus(Message message)
    {
        var propiedad = Assert.IsType<HttpResponseMessageProperty>(
            message.Properties[HttpResponseMessageProperty.Name]);

        return propiedad.StatusCode;
    }

    private sealed class SoapContexto
    {
        public SoapContexto()
        {
            Http = new DefaultHttpContext();
            Http.Request.Headers[_apiKeysOptions.HeaderName] = ApiKey;
            Contribuyentes = new SoapContribuyentesFake(ContribuyenteId, CuitEmisor);
            Emision = new SoapEmisionFake
            {
                Respuesta = new ComprobanteResponse(
                    Guid.NewGuid(),
                    11,
                    "Factura C",
                    1,
                    7,
                    new DateTime(2026, 11, 3, 0, 0, 0, DateTimeKind.Utc),
                    "12345678901234",
                    new DateTime(2026, 11, 13, 0, 0, 0, DateTimeKind.Utc),
                    new ReceptorComprobanteResponse("99", "0", string.Empty, 5),
                    100m,
                    0m,
                    100m,
                    "ARS",
                    "Emitido")
            };
            Autenticador = new SoapApiKeyAuthenticatorFake
            {
                Resultado = new ResultadoApiKey(Guid.NewGuid(), ContribuyenteId)
            };

            var usageRegistrar = new ApiKeyUsageRegistrar(
                new SoapScopeFactoryFake(),
                NullLogger<ApiKeyUsageRegistrar>.Instance,
                _apiKeysOptions);

            Contrato = new ContractFecae(
                Autenticador,
                usageRegistrar,
                Contribuyentes,
                Emision,
                new HttpContextAccessor { HttpContext = Http },
                _apiKeysOptions);
        }

        public Guid ContribuyenteId { get; } = Guid.NewGuid();

        public DefaultHttpContext Http { get; }

        public SoapApiKeyAuthenticatorFake Autenticador { get; }

        public SoapContribuyentesFake Contribuyentes { get; }

        public SoapEmisionFake Emision { get; }

        public ContractFecae Contrato { get; }

        public FECAESolicitarRequest Solicitud(
            string? cuit = CuitEmisor,
            string? idempotencyKey = "idem-1")
            => new()
            {
                Auth = new FECAEAuth { Token = "token-del-sobre" },
                Cuit = cuit,
                PtoVta = 1,
                TipoComprobante = 11,
                Concepto = 1,
                DocTipo = 99,
                DocNro = 0,
                CondicionIVA = 5,
                Items =
                [
                    new FecaeItem
                    {
                        Descripcion = "Servicio",
                        Cantidad = 1m,
                        PrecioUnitario = 100m,
                        AlicuotaIVA = 0m
                    }
                ],
                IdempotencyKey = idempotencyKey
            };

        public FECAEGenerarRequest SolicitudGenerar(string? cuit = CuitEmisor)
            => new()
            {
                Auth = new FECAEAuth { Token = "token-del-sobre" },
                Cuit = cuit,
                PtoVta = 1,
                TipoComprobante = 11,
                Concepto = 1,
                DocTipo = 99,
                DocNro = 0,
                CondicionIVA = 5,
                Items =
                [
                    new FecaeItem
                    {
                        Descripcion = "Servicio",
                        Cantidad = 1m,
                        PrecioUnitario = 100m,
                        AlicuotaIVA = 0m
                    }
                ]
            };

        private readonly ApiKeysOptions _apiKeysOptions = new()
        {
            HeaderName = "X-Api-Key",
            ActualizarUltimoUso = false
        };
    }

    private sealed class SoapApiKeyAuthenticatorFake : IApiKeyAuthenticator
    {
        public ResultadoApiKey? Resultado { get; set; }

        public string? UltimoValor { get; private set; }

        public Task<ResultadoApiKey?> AutenticarAsync(string valorPresentado, CancellationToken ct = default)
        {
            UltimoValor = valorPresentado;
            return Task.FromResult(Resultado);
        }
    }

    private sealed class SoapContribuyentesFake : IContribuyenteRepository
    {
        private readonly Contribuyente _contribuyente;

        public SoapContribuyentesFake(Guid id, string cuit)
            => _contribuyente = new Contribuyente
            {
                Id = id,
                Cuit = cuit,
                RazonSocial = "Demo SRL",
                CondicionIva = CondicionIva.ResponsableInscripto,
                Activo = true
            };

        public int Consultas { get; private set; }

        public Task<Contribuyente?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            Consultas++;
            return Task.FromResult<Contribuyente?>(id == _contribuyente.Id ? _contribuyente : null);
        }

        public Task<bool> ExistePorCuitAsync(string cuit, CancellationToken ct = default)
            => Task.FromResult(string.Equals(cuit, _contribuyente.Cuit, StringComparison.Ordinal));

        public Task InsertarAsync(Contribuyente contribuyente, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private sealed class SoapScopeFactoryFake : IServiceScopeFactory
    {
        public IServiceScope CreateScope()
            => throw new InvalidOperationException("No debe crear un scope con UltimoUso deshabilitado.");
    }

    private sealed class SoapEmisionFake : IEmitirComprobanteService
    {
        public ComprobanteResponse? Respuesta { get; set; }

        public Exception? ErrorEmitir { get; set; }

        public Exception? ErrorValidar { get; set; }

        public bool EmitirLlamado { get; private set; }

        public bool ValidarLlamado { get; private set; }

        public Guid? UltimoContribuyenteId { get; private set; }

        public string? UltimoRol { get; private set; }

        public EmitirComprobanteRequest? UltimoRequest { get; private set; }

        public string? UltimoIdempotencyKey { get; private set; }

        public Task<ResultadoEmision> EmitirAsync(
            Guid contribuyenteId,
            string? rol,
            EmitirComprobanteRequest request,
            string idempotencyKey,
            CancellationToken ct = default)
        {
            EmitirLlamado = true;
            UltimoContribuyenteId = contribuyenteId;
            UltimoRol = rol;
            UltimoRequest = request;
            UltimoIdempotencyKey = idempotencyKey;

            if (ErrorEmitir is not null)
            {
                throw ErrorEmitir;
            }

            return Task.FromResult(new ResultadoEmision(Respuesta!, false));
        }

        public Task ValidarAsync(
            Guid contribuyenteId,
            string? rol,
            EmitirComprobanteRequest request,
            CancellationToken ct = default)
        {
            ValidarLlamado = true;
            UltimoContribuyenteId = contribuyenteId;
            UltimoRol = rol;
            UltimoRequest = request;

            if (ErrorValidar is not null)
            {
                throw ErrorValidar;
            }

            return Task.CompletedTask;
        }
    }
}
