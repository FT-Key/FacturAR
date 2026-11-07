using FacturAR.Contracts.Sistema;
using FacturAR.Web.Auth;
using FacturAR.Web.Components;
using FacturAR.Web.Endpoints;
using FacturAR.Web.Services;
using FacturAR.Web.Services.Asistente;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddInMemoryCollection(AsistenteOptions.VariablesEntorno());

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddRazorPages();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ApiKeyValorState>();
builder.Services.AddHttpClient<FacturarApiClient>(client =>
{
    var baseUrl = builder.Configuration["Api:BaseUrl"] ?? "http://localhost:5171";
    client.BaseAddress = new Uri($"{baseUrl.TrimEnd('/')}/");
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddOptions<AsistenteOptions>()
    .BindConfiguration(AsistenteOptions.SectionName)
    .Validate(
        options => !options.Habilitado || !string.IsNullOrWhiteSpace(options.GroqApiKey),
        "El asistente está habilitado pero falta la clave de Groq. Configurá Asistente:GroqApiKey o la variable GROQ_API_KEY.")
    .ValidateOnStart();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<AsistenteLimites>();
builder.Services.AddScoped<AsistenteAltasService>();
builder.Services.AddHttpClient<IGroqChatClient, GroqChatClient>((services, client) =>
{
    var options = services.GetRequiredService<IOptions<AsistenteOptions>>().Value;
    client.Timeout = TimeSpan.FromSeconds(Math.Max(1, options.TimeoutSegundos));
});

builder.Services.AddScoped<AuthenticationStateProvider, ApiAuthenticationStateProvider>();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthentication(SesionApiAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, SesionApiAuthenticationHandler>(
        SesionApiAuthenticationHandler.SchemeName,
        _ => { });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("EmpresaAutenticado", policy => policy
        .RequireAuthenticatedUser()
        .RequireClaim("contribuyenteId"));

    options.AddPolicy("EmpresaAdministrador", policy => policy
        .RequireAuthenticatedUser()
        .RequireClaim("contribuyenteId")
        .RequireClaim("rol", "Administrador"));

    options.AddPolicy("EmpresaEmisor", policy => policy
        .RequireAuthenticatedUser()
        .RequireClaim("contribuyenteId")
        .RequireClaim("rol", "Administrador", "Emisor"));

    options.AddPolicy("EmpresaConsulta", policy => policy
        .RequireAuthenticatedUser()
        .RequireClaim("contribuyenteId")
        .RequireClaim("rol", "Administrador", "Emisor", "Consulta"));

    options.AddPolicy("AdminGlobal", policy => policy
        .RequireAuthenticatedUser()
        .RequireClaim("rolGlobal", "SuperAdmin", "Soporte"));

    options.AddPolicy("AdminSuper", policy => policy
        .RequireAuthenticatedUser()
        .RequireClaim("rolGlobal", "SuperAdmin"));
});

var buildInformation = new BuildInformation(
    builder.Configuration[BuildInformation.VersionKey] ?? BuildInformation.DefaultVersion,
    builder.Configuration[BuildInformation.CommitKey] ?? BuildInformation.UnknownCommit);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.Use(async (context, next) =>
{
    if (context.Request.Path == "/activar")
    {
        context.Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
    }

    await next(context);
});

app.MapStaticAssets();
app.MapRazorPages();
app.MapSeoEndpoints();
if (builder.Configuration.GetValue<bool>($"{AsistenteOptions.SectionName}:Habilitado"))
{
    app.MapAsistenteEndpoints();
}
else
{
    app.MapPost("/asistente/mensajes", () => Results.Json(
        new AsistenteErrorResponse("ASISTENTE_DESHABILITADO", "El asistente no está disponible."),
        statusCode: StatusCodes.Status404NotFound));
}
app.MapComprobantePdfProxy();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapGet("/health", () => Results.Ok(new EstadoSaludResponse(
    "ok",
    "ok",
    DateTime.UtcNow,
    buildInformation.Version,
    buildInformation.Commit)))
.WithTags("Sistema")
.Produces<EstadoSaludResponse>();

app.MapGet("/health/live", () => Results.Ok(new EstadoVivoResponse(
    "ok",
    buildInformation.Version,
    buildInformation.Commit)))
.WithTags("Sistema")
.Produces<EstadoVivoResponse>();

app.Run();

public partial class Program;