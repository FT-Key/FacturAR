extern alias WebProgram;

using System.Globalization;
using AsistenteWeb = WebProgram::FacturAR.Web.Services.Asistente;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FacturAR.IntegrationTests;

public sealed class PortalWebApplicationFactory : WebApplicationFactory<WebProgram::Program>
{
    private readonly Dictionary<string, string> _configuracion = new(StringComparer.OrdinalIgnoreCase);

    public PortalWebApplicationFactory()
    {
        _configuracion["Portal:BaseUrl"] = "http://localhost";
    }

    public FakeGroqChatClient Groq { get; } = new();

    public PortalWebApplicationFactory Configurar(string clave, string valor)
    {
        _configuracion[clave] = valor;
        return this;
    }

    public PortalWebApplicationFactory HabilitarAsistente(int ratePorMinutoPorIp = 100)
    {
        Configurar("Asistente:Habilitado", "true");
        Configurar("Asistente:GroqApiKey", "clave-de-prueba");
        Configurar("Asistente:OrigenPermitido", "http://localhost");
        Configurar("Asistente:RatePorMinutoPorIp", ratePorMinutoPorIp.ToString(CultureInfo.InvariantCulture));
        Configurar("Asistente:RatePorHoraPorIp", "1000");
        Configurar("Asistente:RatePorDiaPorIp", "1000");
        Configurar("Asistente:RatePorHoraPorSesion", "1000");
        return this;
    }

    public HttpClient CrearCliente()
        => CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseContentRoot(ObtenerContentRoot());

        foreach (var par in _configuracion)
        {
            builder.UseSetting(par.Key, par.Value);
        }

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<AsistenteWeb::IGroqChatClient>();
            services.AddSingleton<AsistenteWeb::IGroqChatClient>(Groq);
        });
    }

    internal static string ObtenerContentRoot()
    {
        var directorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (directorio is not null)
        {
            var candidato = Path.Combine(directorio.FullName, "facturAR_frontend", "src", "FacturAR.Web");
            if (Directory.Exists(candidato))
            {
                return candidato;
            }

            directorio = directorio.Parent;
        }

        throw new InvalidOperationException("No se encontró la carpeta del proyecto FacturAR.Web.");
    }
}

public sealed class FakeGroqChatClient : AsistenteWeb::IGroqChatClient
{
    public int Llamadas { get; private set; }

    public List<IReadOnlyList<AsistenteWeb::GroqMensaje>> Recibidos { get; } = [];

    public IReadOnlyList<string> Contenidos => Recibidos
        .SelectMany(mensajes => mensajes)
        .Select(mensaje => mensaje.Contenido)
        .ToList();

    public AsistenteWeb::GroqRespuesta Respuesta { get; set; } =
        new("No. Primero se solicita el alta de la empresa y después activás la cuenta.", 25);

    public Exception? Excepcion { get; private set; }

    public void FallarConNoDisponible(string mensaje = "El proveedor no responde.")
        => Excepcion = new AsistenteWeb::AsistenteNoDisponibleException(mensaje);

    public Task<AsistenteWeb::GroqRespuesta> ResponderAsync(
        IReadOnlyList<AsistenteWeb::GroqMensaje> mensajes,
        CancellationToken cancellationToken)
    {
        Llamadas++;
        Recibidos.Add(mensajes);

        return Excepcion is null
            ? Task.FromResult(Respuesta)
            : Task.FromException<AsistenteWeb::GroqRespuesta>(Excepcion);
    }
}
