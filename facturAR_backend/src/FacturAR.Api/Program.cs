using FacturAR.Application.Abstractions;
using FacturAR.Application.Comprobantes;
using FacturAR.Contracts.Comunes;
using FacturAR.Contracts.Comprobantes;
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

MongoSerialization.Configure();

builder.Services.AddSingleton(mongoOptions);
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

app.MapGet("/", () => Results.Ok(new
{
    servicio = "FacturAR API",
    version = "0.1.0",
    documentacion = "/openapi/v1.json"
}))
.WithTags("Sistema");

app.MapGet("/health", async (IMongoDatabase database, CancellationToken cancellationToken) =>
{
    try
    {
        await database.RunCommandAsync<BsonDocument>(
            new BsonDocument("ping", 1),
            cancellationToken: cancellationToken);

        return Results.Ok(new
        {
            status = "ok",
            database = "ok",
            timestamp = DateTime.UtcNow
        });
    }
    catch
    {
        return Results.Json(new
        {
            status = "degraded",
            database = "unavailable",
            timestamp = DateTime.UtcNow
        }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
})
.WithTags("Sistema");

app.MapGet("/v1/comprobantes/verificar/{cae}", async (
    string cae,
    VerificarComprobanteService service,
    CancellationToken cancellationToken) =>
{
    if (!CaeValidator.IsValid(cae))
    {
        return Results.BadRequest(new ErrorResponse(
            "CAE_INVALIDO",
            "El CAE debe contener exactamente 14 dígitos."));
    }

    var comprobante = await service.VerificarAsync(cae, cancellationToken);
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
