extern alias WebProgram;

using System.Net;
using System.Text;
using System.Text.Json;
using FacturAR.Contracts.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WebFacturarApiClient = WebProgram::FacturAR.Web.Services.FacturarApiClient;

namespace FacturAR.IntegrationTests;

public sealed class PortalSesionWebApplicationFactory : WebApplicationFactory<WebProgram::Program>
{
    public SesionApiStubHandler Api { get; } = new();

    public HttpClient CrearCliente()
        => CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseContentRoot(PortalWebApplicationFactory.ObtenerContentRoot());
        builder.UseSetting("Portal:BaseUrl", "http://localhost");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<WebFacturarApiClient>();
            services.AddSingleton(new WebFacturarApiClient(
                new HttpClient(Api) { BaseAddress = new Uri("http://api.test/") }));
        });
    }
}

public sealed class SesionApiStubHandler : HttpMessageHandler
{
    private static readonly JsonSerializerOptions OpcionesJson = new(JsonSerializerDefaults.Web);

    public Guid UsuarioId { get; } = Guid.Parse("11111111-1111-4111-8111-111111111111");

    public Guid ContribuyenteId { get; } = Guid.Parse("22222222-2222-4222-8222-222222222222");

    public Guid AdministradorId { get; } = Guid.Parse("33333333-3333-4333-8333-333333333333");

    public bool Disponible { get; set; } = true;

    public List<string> Rutas { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request.RequestUri is null)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest));
        }

        var ruta = request.RequestUri.AbsolutePath;
        Rutas.Add(ruta);

        if (!Disponible)
        {
            throw new HttpRequestException("El servicio no está disponible.");
        }

        return Task.FromResult(ruta switch
        {
            "/v1/auth/me" => Json(new SesionEmpresaResponse(
                UsuarioId,
                "empresa@facturar.test",
                "Administrador",
                ContribuyenteId,
                "Empresa Demo S.A.",
                DateTime.UtcNow.AddHours(8))),
            "/v1/admin/auth/me" => Json(new SesionAdminResponse(
                AdministradorId,
                "admin@facturar.test",
                "Admin Demo",
                "SuperAdmin",
                DateTime.UtcNow.AddHours(2))),
            "/v1/auth/logout" or "/v1/admin/auth/logout" => new HttpResponseMessage(HttpStatusCode.NoContent),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound)
        });
    }

    private static HttpResponseMessage Json<T>(T valor)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(valor, OpcionesJson),
                Encoding.UTF8,
                "application/json")
        };
}
