using FacturAR.Application.Abstractions;
using FacturAR.Api.Auth;
using FacturAR.Contracts.Comprobantes;
using FacturAR.Contracts.Comunes;
using FacturAR.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace FacturAR.Api.Comprobantes;

public static class ComprobanteEndpoints
{
    internal const string HeaderIdempotencia = "Idempotency-Key";
    private const string ClaimRol = "rol";
    private const int LongitudMaximaIdempotencia = 128;
    private const string MensajeMongoNoDisponible = "No hay conexión con la base de datos. Intentá de nuevo en unos minutos.";
    private const string CategoriaLogger = "FacturAR.Api.Comprobantes";

    public static IEndpointRouteBuilder MapComprobanteEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/v1/comprobantes", EmitirAsync)
            .WithName("EmitirComprobante")
            .WithTags("Comprobantes")
            .RequireApiKey()
            .Produces<ComprobanteResponse>(StatusCodes.Status200OK)
            .Produces<ComprobanteResponse>(StatusCodes.Status201Created)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ErrorResponse>(StatusCodes.Status403Forbidden)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ErrorResponse>(StatusCodes.Status409Conflict)
            .Produces<ErrorResponse>(StatusCodes.Status500InternalServerError)
            .Produces<ErrorResponse>(StatusCodes.Status503ServiceUnavailable);

        return app;
    }

    private static async Task<IResult> EmitirAsync(
        EmitirComprobanteRequest request,
        [FromHeader(Name = HeaderIdempotencia)] string? idempotencyKey,
        HttpContext httpContext,
        ITenantContext tenant,
        IEmitirComprobanteService service,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Error(StatusCodes.Status400BadRequest, "VALIDACION", "Falta el header Idempotency-Key.");
        }

        if (idempotencyKey.Length > LongitudMaximaIdempotencia)
        {
            return Error(
                StatusCodes.Status400BadRequest,
                "VALIDACION",
                "El header Idempotency-Key admite hasta 128 caracteres.");
        }

        if (idempotencyKey.Any(char.IsControl))
        {
            return Error(
                StatusCodes.Status400BadRequest,
                "VALIDACION",
                "El header Idempotency-Key no admite caracteres de control.");
        }

        if (tenant.ContribuyenteId is not Guid contribuyenteId)
        {
            return Error(StatusCodes.Status401Unauthorized, "API_KEY_INVALIDA", "La API key no es válida.");
        }

        var rol = httpContext.User.FindFirst(ClaimRol)?.Value;

        try
        {
            var resultado = await service.EmitirAsync(
                contribuyenteId,
                rol,
                request,
                idempotencyKey,
                cancellationToken);

            return resultado.EsRepeticion
                ? Results.Ok(resultado.Comprobante)
                : Results.Created($"/v1/comprobantes/{resultado.Comprobante.Id}", resultado.Comprobante);
        }
        catch (ComprobanteValidationException ex)
        {
            return Error(StatusCodes.Status400BadRequest, "VALIDACION", ex.Message);
        }
        catch (TipoNotaIncompatibleException ex)
        {
            return Error(StatusCodes.Status400BadRequest, "TIPO_NOTA_INCOMPATIBLE", ex.Message);
        }
        catch (ImporteExcedidoException ex)
        {
            return Error(StatusCodes.Status400BadRequest, "IMPORTE_EXCEDIDO", ex.Message);
        }
        catch (ComprobanteAnuladoException ex)
        {
            return Error(StatusCodes.Status409Conflict, "COMPROBANTE_ANULADO", ex.Message);
        }
        catch (EmisionInvalidaException ex)
        {
            return Error(StatusCodes.Status400BadRequest, "EMISION_INVALIDA", ex.Message);
        }
        catch (CuitInvalidoException ex)
        {
            return Error(StatusCodes.Status400BadRequest, "CUIT_INVALIDO", ex.Message);
        }
        catch (SinPermisosException ex)
        {
            return Error(StatusCodes.Status403Forbidden, "SIN_PERMISOS", ex.Message);
        }
        catch (CuentaInactivaException)
        {
            return Error(StatusCodes.Status403Forbidden, "CUENTA_INACTIVA", "El contribuyente está inactivo.");
        }
        catch (ComprobanteNoEncontradoException ex)
        {
            return Error(StatusCodes.Status404NotFound, "COMPROBANTE_NO_ENCONTRADO", ex.Message);
        }
        catch (PuntoVentaInactivoException ex)
        {
            return Error(StatusCodes.Status409Conflict, "PUNTO_VENTA_INACTIVO", ex.Message);
        }
        catch (IdempotenciaConflictoException ex)
        {
            return Error(StatusCodes.Status409Conflict, "IDEMPOTENCIA_CONFLICTO", ex.Message);
        }
        catch (Exception ex) when (ex is MongoConnectionException or TimeoutException)
        {
            return Error(StatusCodes.Status503ServiceUnavailable, "MONGO_NO_DISPONIBLE", MensajeMongoNoDisponible);
        }
        catch (Exception ex)
        {
            loggerFactory
                .CreateLogger(CategoriaLogger)
                .LogError(ex, "Error inesperado al emitir un comprobante para el contribuyente {ContribuyenteId}.", contribuyenteId);

            return Error(
                StatusCodes.Status500InternalServerError,
                "ERROR_INTERNO",
                "Ocurrió un error inesperado.");
        }
    }

    private static IResult Error(int statusCode, string codigo, string mensaje)
        => Results.Json(new ErrorResponse(codigo, mensaje), statusCode: statusCode);
}
