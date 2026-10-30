using System.Threading.RateLimiting;
using FacturAR.Api.Auth;
using FacturAR.Api.Security;
using FacturAR.Api.Solicitudes;
using FacturAR.Application.Abstractions;
using FacturAR.Application.Auth;
using FacturAR.Application.Comprobantes;
using FacturAR.Application.Notificaciones;
using FacturAR.Application.Solicitudes;
using FacturAR.Contracts.Comunes;
using FacturAR.Contracts.Comprobantes;
using FacturAR.Contracts.Sistema;
using FacturAR.Domain.Validation;
using FacturAR.Infrastructure.LoginIntent;
using FacturAR.Infrastructure.Mongo;
using FacturAR.Infrastructure.Notificaciones;
using FacturAR.Infrastructure.Password;
using FacturAR.Infrastructure.Repositories;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.RateLimiting;
using MongoDB.Bson;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

var mongoOptions = builder.Configuration
    .GetSection(MongoOptions.SectionName)
    .Get<MongoOptions>() ?? new MongoOptions();

var authOptions = builder.Configuration
    .GetSection("Auth")
    .Get<AuthOptions>() ?? new AuthOptions();

var bootstrapAdminOptions = builder.Configuration
    .GetSection("BootstrapAdmin")
    .Get<BootstrapAdminOptions>() ?? new BootstrapAdminOptions();

var buildInformation = new BuildInformation(
    builder.Configuration[BuildInformation.VersionKey] ?? BuildInformation.DefaultVersion,
    builder.Configuration[BuildInformation.CommitKey] ?? BuildInformation.UnknownCommit);

var emailOptions = builder.Configuration
    .GetSection(EmailOptions.SectionName)
    .Get<EmailOptions>() ?? new EmailOptions();

if (string.IsNullOrWhiteSpace(emailOptions.Provider))
{
    emailOptions.Provider = builder.Environment.IsDevelopment() ? "File" : "Smtp";
}

var notificacionesOptions = new NotificacionesOptions
{
    PortalBaseUrl = builder.Configuration["Portal:BaseUrl"] ?? "http://localhost:5116",
    AdminNotificationEmail = builder.Configuration["Admin:NotificationEmail"]
};

var solicitudAltaRateLimitOptions = builder.Configuration
    .GetSection(SolicitudAltaRateLimitOptions.SectionName)
    .Get<SolicitudAltaRateLimitOptions>() ?? new SolicitudAltaRateLimitOptions();

NotificacionesConfigurationValidator.Validar(emailOptions, notificacionesOptions, builder.Environment.IsDevelopment());

MongoSerialization.Configure();

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(mongoOptions);
builder.Services.AddSingleton(authOptions);
builder.Services.AddSingleton(bootstrapAdminOptions);
builder.Services.AddSingleton(buildInformation);
builder.Services.AddSingleton(emailOptions);
builder.Services.AddSingleton(notificacionesOptions);
builder.Services.AddSingleton(solicitudAltaRateLimitOptions);
builder.Services.AddSingleton<SolicitudAltaRateLimiter>();

if (string.Equals(emailOptions.Provider, "Smtp", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
}
else
{
    builder.Services.AddSingleton<IEmailSender, FileEmailSender>();
}

builder.Services.AddScoped<EmailService>();
builder.Services.AddSingleton<IMongoClient>(_ => new MongoClient(mongoOptions.ConnectionString));
builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<IMongoClient>().GetDatabase(mongoOptions.DatabaseName));
builder.Services.AddSingleton<MongoDbContext>();

builder.Services.AddScoped<IComprobanteRepository, ComprobanteRepository>();
builder.Services.AddScoped<VerificarComprobanteService>();
builder.Services.AddScoped<IUsuarioRepository, UsuarioRepository>();
builder.Services.AddScoped<IAdministradorRepository, AdministradorRepository>();
builder.Services.AddScoped<IContribuyenteRepository, ContribuyenteRepository>();
builder.Services.AddScoped<IEventoAuditoriaRepository, EventoAuditoriaRepository>();
builder.Services.AddScoped<ISolicitudAltaRepository, SolicitudAltaRepository>();
builder.Services.AddScoped<CrearSolicitudAltaService>();
builder.Services.AddSingleton<IPasswordHasher>(_ => new PasswordHasher(authOptions.PasswordHashIterations));
builder.Services.AddSingleton<ILoginIntentStore>(_ =>
    new LoginIntentStore(authOptions.Login.MaxIntentosFallidos, TimeSpan.FromMinutes(authOptions.Login.MinutosBloqueo)));
builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddScoped<AutenticarEmpresaService>();
builder.Services.AddScoped<AutenticarAdminGlobalService>();
builder.Services.AddScoped<RegistrarAccesoService>();
builder.Services.AddScoped<AdminGlobalBootstrapService>();
builder.Services.AddHostedService<AdminGlobalBootstrapHostedService>();

builder.Services.AddFacturarCookies(authOptions, builder.Environment.IsDevelopment());

builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("login", context =>
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "desconocido";

        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = authOptions.Login.RateLimitPorIp,
            Window = TimeSpan.FromMinutes(authOptions.Login.VentanaRateLimitMinutos),
            QueueLimit = 0
        });
    });

    options.OnRejected = async (context, cancellationToken) =>
    {
        if (context.HttpContext.Response.HasStarted)
        {
            return;
        }

        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(
            new ErrorResponse("RATE_LIMIT", "Se superó el límite de intentos de inicio de sesión."),
            cancellationToken);
    };
});

var app = builder.Build();

app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var excepcion = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    var esSolicitudInvalida = excepcion is BadHttpRequestException;

    context.Response.StatusCode = esSolicitudInvalida
        ? StatusCodes.Status400BadRequest
        : StatusCodes.Status500InternalServerError;
    context.Response.ContentType = "application/json";

    var error = esSolicitudInvalida
        ? new ErrorResponse("VALIDACION", "El cuerpo de la solicitud no es válido.")
        : new ErrorResponse("ERROR_INTERNO", "Ocurrió un error inesperado.");

    await context.Response.WriteAsJsonAsync(error);
}));
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseWhen(
    ctx => string.Equals(
            ctx.Request.Path.Value?.TrimEnd('/'),
            "/v1/solicitudes-alta",
            StringComparison.OrdinalIgnoreCase)
        && HttpMethods.IsPost(ctx.Request.Method),
    branch => branch.UseMiddleware<SolicitudAltaRateLimitMiddleware>());

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

app.MapAuthEndpoints();
app.MapSolicitudAltaEndpoints();

app.Run();

public partial class Program;