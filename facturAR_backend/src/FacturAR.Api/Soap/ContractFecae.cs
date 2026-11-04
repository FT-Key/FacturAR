using FacturAR.Api.Auth;
using FacturAR.Application.Abstractions;
using FacturAR.Application.ApiKeys;
using FacturAR.Contracts.Soap;
using FacturAR.Domain.Exceptions;
using FacturAR.Domain.Validation;
using MongoDB.Driver;

namespace FacturAR.Api.Soap;

public sealed class ContractFecae : IContractFecae
{
    private const int LongitudMaximaIdempotencia = 128;
    private const string PrefijoIdempotenciaSoap = "soap-";
    private const string MensajeApiKeyInvalida = "La API key no es válida.";
    private const string MensajeMongoNoDisponible = "No hay conexión con la base de datos. Intentá de nuevo en unos minutos.";
    private const string MensajeCuitInvalido = "El CUIT informado no es válido.";
    private const string MensajeAislamiento = "El CUIT informado no corresponde a la empresa autenticada.";
    private const string MensajeIdempotencia = "La clave de idempotencia admite hasta 128 caracteres.";
    private const string MensajeErrorInterno = "Ocurrió un error inesperado.";

    private readonly IApiKeyAuthenticator _apiKeys;
    private readonly ApiKeyUsageRegistrar _usageRegistrar;
    private readonly IContribuyenteRepository _contribuyentes;
    private readonly IEmitirComprobanteService _emision;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ApiKeysOptions _apiKeysOptions;

    public ContractFecae(
        IApiKeyAuthenticator apiKeys,
        ApiKeyUsageRegistrar usageRegistrar,
        IContribuyenteRepository contribuyentes,
        IEmitirComprobanteService emision,
        IHttpContextAccessor httpContextAccessor,
        ApiKeysOptions apiKeysOptions)
    {
        _apiKeys = apiKeys;
        _usageRegistrar = usageRegistrar;
        _contribuyentes = contribuyentes;
        _emision = emision;
        _httpContextAccessor = httpContextAccessor;
        _apiKeysOptions = apiKeysOptions;
    }

    public async Task<FECAESolicitarResponse> FECAESolicitar(FECAESolicitarRequest request)
    {
        var ct = ObtenerContexto().RequestAborted;

        try
        {
            var autenticado = await AutenticarAsync(request.Auth?.Token, ct);

            var errorAislamiento = await VerificarAislamientoAsync(autenticado.ContribuyenteId, request.Cuit, ct);
            if (errorAislamiento is not null)
            {
                return FecaeMapeador.ARechazada(errorAislamiento.Codigo, errorAislamiento.Mensaje);
            }

            var respuesta = await _emision.EmitirAsync(
                autenticado.ContribuyenteId,
                null,
                FecaeMapeador.AEmision(request),
                ResolverIdempotencyKey(request.IdempotencyKey),
                ct);

            return FecaeMapeador.AAprobada(respuesta.Comprobante);
        }
        catch (Exception ex) when (EsErrorDeNegocio(ex))
        {
            return FecaeMapeador.ARechazada(CodigoDeError(ex), ex.Message);
        }
    }

    public async Task<FECAEGenerarResponse> FECAEGenerar(FECAEGenerarRequest request)
    {
        var ct = ObtenerContexto().RequestAborted;

        try
        {
            var autenticado = await AutenticarAsync(request.Auth?.Token, ct);

            var errorAislamiento = await VerificarAislamientoAsync(autenticado.ContribuyenteId, request.Cuit, ct);
            if (errorAislamiento is not null)
            {
                return FecaeMapeador.GenerarRechazada(errorAislamiento.Codigo, errorAislamiento.Mensaje);
            }

            await _emision.ValidarAsync(
                autenticado.ContribuyenteId,
                null,
                FecaeMapeador.AEmision(request),
                ct);

            return FecaeMapeador.GenerarAprobada();
        }
        catch (Exception ex) when (EsErrorDeNegocio(ex))
        {
            return FecaeMapeador.GenerarRechazada(CodigoDeError(ex), ex.Message);
        }
    }

    private async Task<ResultadoApiKey> AutenticarAsync(string? tokenSobre, CancellationToken ct)
    {
        var contexto = ObtenerContexto();
        var valorHeader = contexto.Request.Headers[_apiKeysOptions.HeaderName].ToString();
        var valor = string.IsNullOrWhiteSpace(valorHeader) ? tokenSobre : valorHeader;

        if (string.IsNullOrWhiteSpace(valor))
        {
            throw new FecaeFaultException(
                StatusCodes.Status401Unauthorized,
                "API_KEY_AUSENTE",
                $"Falta el header {_apiKeysOptions.HeaderName}.");
        }

        ResultadoApiKey? resultado;
        try
        {
            resultado = await _apiKeys.AutenticarAsync(valor, ct);
        }
        catch (Exception ex) when (ex is MongoConnectionException or TimeoutException)
        {
            throw new FecaeFaultException(
                StatusCodes.Status503ServiceUnavailable,
                "MONGO_NO_DISPONIBLE",
                MensajeMongoNoDisponible);
        }

        if (resultado is null)
        {
            throw new FecaeFaultException(
                StatusCodes.Status401Unauthorized,
                "API_KEY_INVALIDA",
                MensajeApiKeyInvalida);
        }

        _usageRegistrar.Registrar(resultado.ApiKeyId, resultado.ContribuyenteId);

        return resultado;
    }

    private async Task<ErrorNegocio?> VerificarAislamientoAsync(
        Guid contribuyenteId,
        string? cuit,
        CancellationToken ct)
    {
        if (!CuitValidator.TryNormalize(cuit, out var cuitNormalizado))
        {
            return new ErrorNegocio("CUIT_INVALIDO", MensajeCuitInvalido);
        }

        var contribuyente = await _contribuyentes.GetByIdAsync(contribuyenteId, ct);
        if (contribuyente is null)
        {
            throw new FecaeFaultException(
                StatusCodes.Status401Unauthorized,
                "API_KEY_INVALIDA",
                MensajeApiKeyInvalida);
        }

        return string.Equals(CuitValidator.Normalize(contribuyente.Cuit), cuitNormalizado, StringComparison.Ordinal)
            ? null
            : new ErrorNegocio("VALIDACION", MensajeAislamiento);
    }

    private static string ResolverIdempotencyKey(string? idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return $"{PrefijoIdempotenciaSoap}{Guid.NewGuid():N}";
        }

        return idempotencyKey.Length > LongitudMaximaIdempotencia
            ? throw new ComprobanteValidationException(MensajeIdempotencia)
            : idempotencyKey;
    }

    private HttpContext ObtenerContexto()
        => _httpContextAccessor.HttpContext
            ?? throw new FecaeFaultException(
                StatusCodes.Status500InternalServerError,
                "ERROR_INTERNO",
                MensajeErrorInterno);

    private static bool EsErrorDeNegocio(Exception ex)
        => ex is ComprobanteValidationException
            or EmisionInvalidaException
            or CuitInvalidoException
            or SinPermisosException
            or CuentaInactivaException
            or ComprobanteNoEncontradoException
            or PuntoVentaInactivoException
            or IdempotenciaConflictoException
            or TipoNotaIncompatibleException
            or ImporteExcedidoException
            or ComprobanteAnuladoException;

    private static string CodigoDeError(Exception ex) => ex switch
    {
        ComprobanteValidationException => "VALIDACION",
        EmisionInvalidaException => "EMISION_INVALIDA",
        CuitInvalidoException => "CUIT_INVALIDO",
        SinPermisosException => "SIN_PERMISOS",
        CuentaInactivaException => "CUENTA_INACTIVA",
        ComprobanteNoEncontradoException => "COMPROBANTE_NO_ENCONTRADO",
        PuntoVentaInactivoException => "PUNTO_VENTA_INACTIVO",
        IdempotenciaConflictoException => "IDEMPOTENCIA_CONFLICTO",
        TipoNotaIncompatibleException => "TIPO_NOTA_INCOMPATIBLE",
        ImporteExcedidoException => "IMPORTE_EXCEDIDO",
        ComprobanteAnuladoException => "COMPROBANTE_ANULADO",
        _ => "ERROR_INTERNO"
    };

    private sealed record ErrorNegocio(string Codigo, string Mensaje);
}
