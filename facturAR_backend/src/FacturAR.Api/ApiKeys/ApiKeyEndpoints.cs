using FacturAR.Application.Abstractions;
using FacturAR.Application.ApiKeys;
using FacturAR.Contracts.ApiKeys;
using FacturAR.Contracts.Comunes;
using FacturAR.Domain.Exceptions;
using MongoDB.Driver;

namespace FacturAR.Api.ApiKeys;

public static class ApiKeyEndpoints
{
    private const string MensajeSinSesion = "La sesión no es válida o expiró.";
    private const string MensajeMongoNoDisponible = "No hay conexión con la base de datos. Intentá de nuevo en unos minutos.";
    private const string CategoriaLogger = "FacturAR.Api.ApiKeys";

    public static IEndpointRouteBuilder MapApiKeyEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/v1/api-keys")
            .WithTags("API keys")
            .RequireAuthorization("EmpresaAdministrador");

        grupo.MapPost("/", CrearAsync)
            .WithName("CrearApiKey")
            .ConErroresComunes()
            .Produces<ApiKeyCreadaResponse>(StatusCodes.Status201Created)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest);

        grupo.MapGet("/", ListarAsync)
            .WithName("ListarApiKeys")
            .ConErroresComunes()
            .Produces<ApiKeyListResponse>();

        grupo.MapDelete("/{id}", RevocarAsync)
            .WithName("RevocarApiKey")
            .ConErroresComunes()
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        grupo.MapPost("/{id}/regenerar", RegenerarAsync)
            .WithName("RegenerarApiKey")
            .ConErroresComunes()
            .Produces<ApiKeyRegeneradaResponse>()
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        return app;
    }

    private static Task<IResult> CrearAsync(
        CrearApiKeyRequest request,
        ITenantContext tenant,
        CrearApiKeyService service,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
        => ConSesionAsync(tenant, loggerFactory, async (contribuyenteId, usuarioId) =>
        {
            var creada = await service.CrearAsync(
                contribuyenteId,
                usuarioId,
                request.Nombre,
                cancellationToken);

            return Results.Created($"/v1/api-keys/{creada.Id}", creada);
        });

    private static Task<IResult> ListarAsync(
        ITenantContext tenant,
        ListarApiKeysService service,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
        => ConSesionAsync(tenant, loggerFactory, async (contribuyenteId, _) =>
            Results.Ok(await service.ListarAsync(contribuyenteId, cancellationToken)));

    private static Task<IResult> RevocarAsync(
        string id,
        ITenantContext tenant,
        RevocarApiKeyService service,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(id, out var apiKeyId))
        {
            return Task.FromResult(Validacion());
        }

        return ConSesionAsync(tenant, loggerFactory, async (contribuyenteId, usuarioId) =>
        {
            await service.RevocarAsync(apiKeyId, contribuyenteId, usuarioId, cancellationToken);
            return Results.NoContent();
        });
    }

    private static Task<IResult> RegenerarAsync(
        string id,
        ITenantContext tenant,
        RegenerarApiKeyService service,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(id, out var apiKeyId))
        {
            return Task.FromResult(Validacion());
        }

        return ConSesionAsync(tenant, loggerFactory, async (contribuyenteId, usuarioId) =>
            Results.Ok(await service.RegenerarAsync(
                apiKeyId,
                contribuyenteId,
                usuarioId,
                cancellationToken)));
    }

    private static Task<IResult> ConSesionAsync(
        ITenantContext tenant,
        ILoggerFactory loggerFactory,
        Func<Guid, Guid, Task<IResult>> operacion)
    {
        if (tenant.ContribuyenteId is not Guid contribuyenteId || tenant.UsuarioId is not Guid usuarioId)
        {
            return Task.FromResult(Error(
                StatusCodes.Status401Unauthorized,
                "CREDENCIALES_INVALIDAS",
                MensajeSinSesion));
        }

        return EjecutarAsync(loggerFactory.CreateLogger(CategoriaLogger), () => operacion(contribuyenteId, usuarioId));
    }

    private static async Task<IResult> EjecutarAsync(ILogger logger, Func<Task<IResult>> operacion)
    {
        try
        {
            return await operacion();
        }
        catch (ApiKeyValidationException ex)
        {
            return Error(StatusCodes.Status400BadRequest, "VALIDACION", ex.Message);
        }
        catch (ApiKeyNoEncontradaException ex)
        {
            return Error(StatusCodes.Status404NotFound, "API_KEY_INVALIDA", ex.Message);
        }
        catch (CuentaInactivaException)
        {
            return Error(
                StatusCodes.Status403Forbidden,
                "CUENTA_INACTIVA",
                "La empresa no está habilitada para administrar API keys.");
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError(ex, "Error inesperado al gestionar API keys.");
            return Error(
                StatusCodes.Status500InternalServerError,
                "ERROR_INTERNO",
                "Ocurrió un error inesperado.");
        }
        catch (Exception ex) when (ex is MongoConnectionException or TimeoutException)
        {
            return Error(StatusCodes.Status503ServiceUnavailable, "MONGO_NO_DISPONIBLE", MensajeMongoNoDisponible);
        }
    }

    private static RouteHandlerBuilder ConErroresComunes(this RouteHandlerBuilder builder)
        => builder
            .Produces<ErrorResponse>(StatusCodes.Status401Unauthorized)
            .Produces<ErrorResponse>(StatusCodes.Status403Forbidden)
            .Produces<ErrorResponse>(StatusCodes.Status500InternalServerError)
            .Produces<ErrorResponse>(StatusCodes.Status503ServiceUnavailable);

    private static IResult Validacion()
        => Error(StatusCodes.Status400BadRequest, "VALIDACION", "El identificador de la API key no es válido.");

    private static IResult Error(int statusCode, string codigo, string mensaje)
        => Results.Json(new ErrorResponse(codigo, mensaje), statusCode: statusCode);
}
