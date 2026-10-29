using FacturAR.Contracts.Sistema;
using FacturAR.Web.Components;
using FacturAR.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddRazorPages();

builder.Services.AddHttpClient<FacturarApiClient>(client =>
{
    var baseUrl = builder.Configuration["Api:BaseUrl"] ?? "http://localhost:5171";
    client.BaseAddress = new Uri($"{baseUrl.TrimEnd('/')}/");
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
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorPages();
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
