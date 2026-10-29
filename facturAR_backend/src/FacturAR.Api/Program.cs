using FacturAR.Application.Abstractions;
using FacturAR.Application.Comprobantes;
using FacturAR.Contracts.Comunes;
using FacturAR.Contracts.Comprobantes;
using FacturAR.Contracts.Sistema;
using FacturAR.Domain.Validation;
using FacturAR.Infrastructure.Mongo;
using FacturAR.Infrastructure.Repositories;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

var mongoOptions = builder.Configuration
    .GetSection(MongoOptions.SectionName)
    .Get<MongoOptions>() ?? new MongoOptions();

var buildInformation = new BuildInformation(
    builder.Configuration[BuildInformation.VersionKey] ?? BuildInformation.DefaultVersion,
    builder.Configuration[BuildInformation.CommitKey] ?? BuildInformation.UnknownCommit);

MongoSerialization.Configure();

builder.Services.AddSingleton(mongoOptions);
builder.Services.AddSingleton(buildInformation);
builder.Services.AddSingleton<IMongoClient>(_ => new MongoClient(mongoOptions.ConnectionString));
builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<IMongoClient>().GetDatabase(mongoOptions.DatabaseName));
builder.Services.AddSingleton<MongoDbContext>();
builder.Services.AddScoped<IComprobanteRepository, ComprobanteRepository>();
builder.Services.AddScoped<VerificarComprobanteService>();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (mongoOptions.InitializeIndexes)
{
    try
    {
        using var scope = app.Services.CreateScope();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var mongoContext = scope.ServiceProvider.GetRequiredService<MongoDbContext>();
        await MongoIndexInitializer.InitializeAsync(mongoContext, timeout.Token);
        app.Logger.LogInformation("Índices de MongoDB verificados.");
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "No se pudieron inicializar los índices de MongoDB.");
    }
}

app.MapGet("/", (BuildInformation build) => Results.Ok(new
{
    servicio = "FacturAR API",
    version = build.Version,
    commit = build.Commit,
    documentacion = "/openapi/v1.json"
}))
.WithTags("Sistema");

app.MapGet("/health", async (
    IMongoDatabase database,
    BuildInformation build,
    CancellationToken cancellationToken) =>
{
    try
    {
        await database.RunCommandAsync<BsonDocument>(
            new BsonDocument("ping", 1),
            cancellationToken: cancellationToken);

        return Results.Ok(new EstadoSaludResponse(
            "ok",
            "ok",
            DateTime.UtcNow,
            build.Version,
            build.Commit));
    }
    catch
    {
        return Results.Json(
            new EstadoSaludResponse(
                "degraded",
                "unavailable",
                DateTime.UtcNow,
                build.Version,
                build.Commit),
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
})
.WithTags("Sistema")
.Produces<EstadoSaludResponse>()
.Produces<EstadoSaludResponse>(StatusCodes.Status503ServiceUnavailable);

app.MapGet("/health/live", (BuildInformation build) => Results.Ok(new EstadoVivoResponse(
    "ok",
    build.Version,
    build.Commit)))
.WithTags("Sistema")
.Produces<EstadoVivoResponse>();

app.MapGet("/v1/comprobantes/verificar/{cae}", async (
    string cae,
    VerificarComprobanteService service,
    CancellationToken cancellationToken) =>
{
    var caeNormalizado = CaeValidator.Normalize(cae);

    if (!CaeValidator.IsValid(caeNormalizado))
    {
        return Results.BadRequest(new ErrorResponse(
            "CAE_INVALIDO",
            "El CAE debe contener exactamente 14 dígitos."));
    }

    var comprobante = await service.VerificarAsync(caeNormalizado, cancellationToken);
    return comprobante is null
        ? Results.NotFound(new ErrorResponse(
            "COMPROBANTE_NO_ENCONTRADO",
            "No existe un comprobante emitido con el CAE indicado."))
        : Results.Ok(comprobante);
})
.WithName("VerificarComprobante")
.WithTags("Comprobantes")
.Produces<VerificacionResponse>()
.Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
.Produces<ErrorResponse>(StatusCodes.Status404NotFound);

app.Run();

public partial class Program;
