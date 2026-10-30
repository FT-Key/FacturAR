using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FacturAR.Contracts.Solicitudes;
using FacturAR.Domain.Enums;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Core.Clusters;
using MongoDB.Driver.Core.Connections;
using MongoDB.Driver.Core.Servers;

namespace FacturAR.IntegrationTests;

public sealed class SolicitudAltaEndpointsTests
{
    private const string CuitValido = "20123456786";
    private const string EmailValido = "solicitante@demo.com";
    private const string CuerpoExitoso = "{\"codigo\":\"SOLICITUD_RECIBIDA\",\"mensaje\":\"Recibimos tu solicitud. Revisamos los datos y te escribimos al email de contacto.\"}";

    private static int s_ip;

    [Fact]
    public async Task Post_ConSolicitudValida_DevuelveAcceptedConCuerpoGenericoExacto()
    {
        using var factory = new SolicitudAltaWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await PostAsync(
            client,
            Request() with { Cuit = "20-12345678-6", Email = "  Solicitante@Demo.COM  " },
            IpUnica());

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var cuerpo = await response.Content.ReadAsStringAsync();
        Assert.Equal(CuerpoExitoso, cuerpo);
        Assert.DoesNotContain(CuitValido, cuerpo, StringComparison.Ordinal);
        Assert.DoesNotContain(EmailValido, cuerpo, StringComparison.OrdinalIgnoreCase);

        using var documento = JsonDocument.Parse(cuerpo);
        Assert.Equal(2, documento.RootElement.EnumerateObject().Count());
        Assert.True(documento.RootElement.TryGetProperty("codigo", out var codigo));
        Assert.Equal("SOLICITUD_RECIBIDA", codigo.GetString());
        Assert.True(documento.RootElement.TryGetProperty("mensaje", out _));

        var solicitud = Assert.Single(factory.Solicitudes.Insertadas);
        Assert.Equal(CuitValido, solicitud.Cuit);
        Assert.Equal(EmailValido, solicitud.EmailSolicitante);
        Assert.Equal(EstadoSolicitud.Pendiente, solicitud.Estado);
        Assert.True(solicitud.AceptaTerminos);
        Assert.Equal(DateTimeKind.Utc, solicitud.CreatedAt.Kind);

        var mensaje = Assert.Single(factory.Emails.Enviados);
        Assert.Equal(EmailValido, mensaje.Para);
    }

    [Fact]
    public async Task Post_ConCuitInvalido_DevuelveValidacionSinInsertar()
    {
        using var factory = new SolicitudAltaWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await PostAsync(client, Request() with { Cuit = "20123456787" }, IpUnica());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("VALIDACION", error.Codigo);
        Assert.Equal("El CUIT no es válido.", error.Mensaje);
        Assert.Empty(factory.Solicitudes.Insertadas);
        Assert.Empty(factory.Emails.Enviados);
    }

    [Fact]
    public async Task Post_ConEmailInvalido_DevuelveValidacionSinInsertar()
    {
        using var factory = new SolicitudAltaWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await PostAsync(client, Request() with { Email = "sin-arroba" }, IpUnica());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("VALIDACION", error.Codigo);
        Assert.Equal("El email no es válido.", error.Mensaje);
        Assert.Empty(factory.Solicitudes.Insertadas);
    }

    [Fact]
    public async Task Post_ConEmailYRazonSocialInvalidos_DevuelveUnSoloMensajeSinEnumerarCampos()
    {
        using var factory = new SolicitudAltaWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await PostAsync(
            client,
            Request() with { Email = "sin-arroba", RazonSocial = "   " },
            IpUnica());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var cuerpo = await response.Content.ReadAsStringAsync();
        using var documento = JsonDocument.Parse(cuerpo);
        Assert.Equal(2, documento.RootElement.EnumerateObject().Count());
        Assert.False(documento.RootElement.TryGetProperty("errores", out _));

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("VALIDACION", error.Codigo);
        Assert.Equal("La razón social es obligatoria.", error.Mensaje);
        Assert.DoesNotContain("email", error.Mensaje, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(factory.Solicitudes.Insertadas);
        Assert.Empty(factory.Emails.Enviados);
        Assert.Empty(factory.Eventos.Eventos);
    }

    [Fact]
    public async Task Post_SinAceptarTerminos_DevuelveValidacionSinInsertar()
    {
        using var factory = new SolicitudAltaWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await PostAsync(
            client,
            Request() with { AceptaTerminos = false },
            IpUnica());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("VALIDACION", error.Codigo);
        Assert.Equal("Tenés que aceptar los términos para enviar la solicitud.", error.Mensaje);
        Assert.Empty(factory.Solicitudes.Insertadas);
    }

    [Fact]
    public async Task Post_ConCuitPendiente_DevuelveConflictSinInsertar()
    {
        using var factory = new SolicitudAltaWebApplicationFactory();
        using var client = factory.CreateClient();
        factory.Solicitudes.CuitsPendientes.Add(CuitValido);

        var response = await PostAsync(client, Request() with { Email = "otro@demo.com" }, IpUnica());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("SOLICITUD_PENDIENTE", error.Codigo);
        Assert.Empty(factory.Solicitudes.Insertadas);
        Assert.Empty(factory.Emails.Enviados);
    }

    [Fact]
    public async Task Post_ConEmailPendiente_DevuelveConflictSinInsertar()
    {
        using var factory = new SolicitudAltaWebApplicationFactory();
        using var client = factory.CreateClient();
        factory.Solicitudes.EmailsPendientes.Add(EmailValido);

        var response = await PostAsync(
            client,
            Request() with { Cuit = "30712345671" },
            IpUnica());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("SOLICITUD_PENDIENTE", error.Codigo);
        Assert.Empty(factory.Solicitudes.Insertadas);
        Assert.Empty(factory.Emails.Enviados);
    }

    [Fact]
    public async Task Post_ConCuitDeContribuyente_DevuelveMismoAcceptedYAuditaElIntento()
    {
        using var factory = new SolicitudAltaWebApplicationFactory();
        using var client = factory.CreateClient();
        factory.Contribuyentes.Cuits.Add(CuitValido);

        var response = await PostAsync(client, Request(), IpUnica());

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(CuerpoExitoso, await response.Content.ReadAsStringAsync());
        Assert.Empty(factory.Solicitudes.Insertadas);
        Assert.Empty(factory.Emails.Enviados);

        var evento = Assert.Single(factory.Eventos.Eventos);
        Assert.Equal("solicitud.cuit_registrado", evento.Accion);
        Assert.Contains(CuitValido, evento.Datos);
    }

    [Fact]
    public async Task Post_SuperandoElLimitePorIp_DevuelveRateLimitConRetryAfter()
    {
        using var factory = new SolicitudAltaWebApplicationFactory { ConfiarEnProxy = true };
        using var client = factory.CreateClient();
        var ip = IpUnica();

        for (var indice = 0; indice < 5; indice++)
        {
            var aceptada = await PostAsync(
                client,
                Request() with { Email = $"limite-ip-{indice}@demo.com" },
                ip);
            Assert.Equal(HttpStatusCode.Accepted, aceptada.StatusCode);
        }

        var rechazada = await PostAsync(
            client,
            Request() with { Email = "limite-ip-rechazada@demo.com" },
            ip);

        Assert.Equal(HttpStatusCode.TooManyRequests, rechazada.StatusCode);
        Assert.True(rechazada.Headers.TryGetValues("Retry-After", out var valores));
        var segundos = int.Parse(valores.Single(), CultureInfo.InvariantCulture);
        Assert.True(segundos > 0);

        var error = await rechazada.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("RATE_LIMIT", error.Codigo);
        Assert.Equal("Demasiadas solicitudes. Intentá de nuevo más tarde.", error.Mensaje);
        Assert.Equal(5, factory.Solicitudes.Insertadas.Count);
    }

    [Fact]
    public async Task Post_SuperandoElLimitePorEmail_DevuelveRateLimitSinInsertar()
    {
        using var factory = new SolicitudAltaWebApplicationFactory { ConfiarEnProxy = true };
        using var client = factory.CreateClient();
        var email = "limite-email@demo.com";

        for (var indice = 0; indice < 3; indice++)
        {
            var aceptada = await PostAsync(
                client,
                Request() with { Email = email },
                IpUnica());
            Assert.Equal(HttpStatusCode.Accepted, aceptada.StatusCode);
        }

        var rechazada = await PostAsync(client, Request() with { Email = email }, IpUnica());

        Assert.Equal(HttpStatusCode.TooManyRequests, rechazada.StatusCode);
        Assert.True(rechazada.Headers.TryGetValues("Retry-After", out var valores));
        Assert.True(int.Parse(valores.Single(), CultureInfo.InvariantCulture) > 0);

        var error = await rechazada.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("RATE_LIMIT", error.Codigo);
        Assert.Equal(3, factory.Solicitudes.Insertadas.Count);
    }

    [Theory]
    [InlineData("{invalid")]
    [InlineData("")]
    [InlineData("null")]
    public async Task Post_ConCuerpoInvalido_DevuelveValidacionSinInsertar(string cuerpo)
    {
        using var factory = new SolicitudAltaWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await PostRawAsync(client, cuerpo);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("VALIDACION", error.Codigo);
        Assert.Equal("El cuerpo de la solicitud no es válido.", error.Mensaje);
        Assert.Empty(factory.Solicitudes.Insertadas);
        Assert.Empty(factory.Emails.Enviados);
    }

    [Fact]
    public async Task Post_ConBarraFinal_AplicaElRateLimitPorIp()
    {
        using var factory = new SolicitudAltaWebApplicationFactory();
        using var client = factory.CreateClient();

        for (var indice = 0; indice < 5; indice++)
        {
            var aceptada = await PostAsync(
                client,
                Request() with { Email = $"barra-final-{indice}@demo.com" },
                IpUnica(),
                "/v1/solicitudes-alta/");
            Assert.Equal(HttpStatusCode.Accepted, aceptada.StatusCode);
        }

        var rechazada = await PostAsync(
            client,
            Request() with { Email = "barra-final-rechazada@demo.com" },
            IpUnica(),
            "/v1/solicitudes-alta/");

        Assert.Equal(HttpStatusCode.TooManyRequests, rechazada.StatusCode);
        Assert.Equal(5, factory.Solicitudes.Insertadas.Count);
    }

    [Fact]
    public async Task Post_ConConfiarEnProxyFalso_IgnoraXForwardedForYLimitaPorIpReal()
    {
        using var factory = new SolicitudAltaWebApplicationFactory();
        using var client = factory.CreateClient();

        for (var indice = 0; indice < 5; indice++)
        {
            var aceptada = await PostAsync(
                client,
                Request() with { Email = $"sin-proxy-{indice}@demo.com" },
                $"203.0.113.{indice + 1}");
            Assert.Equal(HttpStatusCode.Accepted, aceptada.StatusCode);
        }

        var rechazada = await PostAsync(
            client,
            Request() with { Email = "sin-proxy-rechazada@demo.com" },
            "203.0.113.200");

        Assert.Equal(HttpStatusCode.TooManyRequests, rechazada.StatusCode);
        Assert.Equal(5, factory.Solicitudes.Insertadas.Count);
    }

    [Fact]
    public async Task Post_ConConfiarEnProxyVerdadero_UsaXForwardedForComoParticion()
    {
        using var factory = new SolicitudAltaWebApplicationFactory { ConfiarEnProxy = true };
        using var client = factory.CreateClient();

        for (var indice = 0; indice < 6; indice++)
        {
            var aceptada = await PostAsync(
                client,
                Request() with { Email = $"con-proxy-{indice}@demo.com" },
                $"203.0.113.{indice + 1}");
            Assert.Equal(HttpStatusCode.Accepted, aceptada.StatusCode);
        }

        Assert.Equal(6, factory.Solicitudes.Insertadas.Count);
    }

    [Fact]
    public async Task Post_ConEmailDuplicado_ElRateLimitUsaLaUltimaPropiedad()
    {
        using var factory = new SolicitudAltaWebApplicationFactory();
        using var client = factory.CreateClient();
        const string victima = "victima@demo.com";

        for (var indice = 0; indice < 3; indice++)
        {
            var aceptada = await PostRawAsync(
                client,
                CuerpoConEmailDuplicado($"senuelo-{indice}@demo.com", victima));
            Assert.Equal(HttpStatusCode.Accepted, aceptada.StatusCode);
        }

        var rechazada = await PostRawAsync(
            client,
            CuerpoConEmailDuplicado("senuelo-final@demo.com", victima));

        Assert.Equal(HttpStatusCode.TooManyRequests, rechazada.StatusCode);
        Assert.Equal(3, factory.Solicitudes.Insertadas.Count);
        Assert.All(
            factory.Solicitudes.Insertadas,
            solicitud => Assert.Equal(victima, solicitud.EmailSolicitante));
    }

    [Fact]
    public async Task Post_ConCuerpoMayorA32Kb_DevuelveValidacionSinInsertar()
    {
        using var factory = new SolicitudAltaWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await PostRawAsync(
            client,
            CuerpoConPadding(EmailValido, new string('a', 33 * 1024)),
            IpUnica());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("VALIDACION", error.Codigo);
        Assert.Equal("El cuerpo de la solicitud no es válido.", error.Mensaje);
        Assert.Empty(factory.Solicitudes.Insertadas);
        Assert.Empty(factory.Emails.Enviados);
    }

    [Fact]
    public async Task Post_ConPaddingGrandeValido_AplicaLimitePorEmail()
    {
        using var factory = new SolicitudAltaWebApplicationFactory { ConfiarEnProxy = true };
        using var client = factory.CreateClient();
        const string email = "padding-email@demo.com";
        var cuerpo = CuerpoConPadding(email, new string('a', 30 * 1024));

        for (var indice = 0; indice < 3; indice++)
        {
            var aceptada = await PostRawAsync(client, cuerpo, IpUnica());
            Assert.Equal(HttpStatusCode.Accepted, aceptada.StatusCode);
        }

        var rechazada = await PostRawAsync(client, cuerpo, IpUnica());

        Assert.Equal(HttpStatusCode.TooManyRequests, rechazada.StatusCode);
        var error = await rechazada.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("RATE_LIMIT", error.Codigo);
        Assert.Equal(3, factory.Solicitudes.Insertadas.Count);
    }

    [Fact]
    public async Task Post_ConXForwardedForDeDosEntradas_UsaLaUltimaComoParticion()
    {
        using var factory = new SolicitudAltaWebApplicationFactory { ConfiarEnProxy = true };
        using var client = factory.CreateClient();

        for (var indice = 0; indice < 5; indice++)
        {
            var aceptada = await PostAsync(
                client,
                Request() with { Email = $"xff-a-{indice}@demo.com" },
                "1.1.1.1, 203.0.113.9");
            Assert.Equal(HttpStatusCode.Accepted, aceptada.StatusCode);
        }

        for (var indice = 0; indice < 5; indice++)
        {
            var aceptada = await PostAsync(
                client,
                Request() with { Email = $"xff-b-{indice}@demo.com" },
                "1.1.1.1, 203.0.113.10");
            Assert.Equal(HttpStatusCode.Accepted, aceptada.StatusCode);
        }

        var rechazada = await PostAsync(
            client,
            Request() with { Email = "xff-a-rechazada@demo.com" },
            "1.1.1.1, 203.0.113.9");

        Assert.Equal(HttpStatusCode.TooManyRequests, rechazada.StatusCode);
        Assert.Equal(10, factory.Solicitudes.Insertadas.Count);
    }

    [Fact]
    public async Task Post_ConEmailInvalido_DevuelveValidacionYNoRateLimit()
    {
        using var factory = new SolicitudAltaWebApplicationFactory { ConfiarEnProxy = true };
        using var client = factory.CreateClient();

        for (var indice = 0; indice < 4; indice++)
        {
            var response = await PostAsync(
                client,
                Request() with { Email = "sin-arroba" },
                IpUnica());

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
            Assert.NotNull(error);
            Assert.Equal("VALIDACION", error.Codigo);
            Assert.Equal("El email no es válido.", error.Mensaje);
        }

        Assert.Empty(factory.Solicitudes.Insertadas);
    }

    [Theory]
    [InlineData("timeout")]
    [InlineData("mongo")]
    public async Task Post_ConMongoNoDisponible_DevuelveServiceUnavailable(string tipo)
    {
        using var factory = new SolicitudAltaWebApplicationFactory();
        using var client = factory.CreateClient();
        factory.Solicitudes.Excepcion = tipo == "timeout"
            ? new TimeoutException("timeout de prueba.")
            : MongoNoDisponible();

        var response = await PostAsync(client, Request(), IpUnica());

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("MONGO_NO_DISPONIBLE", error.Codigo);
        Assert.Empty(factory.Solicitudes.Insertadas);
    }

    [Fact]
    public async Task Post_ConMongoCommandException_DevuelveErrorInterno()
    {
        using var factory = new SolicitudAltaWebApplicationFactory();
        using var client = factory.CreateClient();
        factory.Solicitudes.Excepcion = new MongoCommandException(
            new ConnectionId(new ServerId(new ClusterId(1), new DnsEndPoint("localhost", 27017))),
            "falla de comando de prueba.",
            new BsonDocument());

        var response = await PostAsync(client, Request(), IpUnica());

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("ERROR_INTERNO", error.Codigo);
        Assert.Empty(factory.Solicitudes.Insertadas);
    }

    [Fact]
    public async Task Get_SobreSolicitudesAlta_NoExponeLecturaNiListado()
    {
        using var factory = new SolicitudAltaWebApplicationFactory();
        using var client = factory.CreateClient();

        var listado = await client.GetAsync("/v1/solicitudes-alta");
        Assert.True(
            listado.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed,
            $"Se esperaba 404 o 405 y se recibió {(int)listado.StatusCode}.");

        var detalle = await client.GetAsync($"/v1/solicitudes-alta/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, detalle.StatusCode);
    }

    [Fact]
    public async Task OpenApi_DeSolicitudesAlta_SoloExponeElPost()
    {
        using var factory = new SolicitudAltaWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var documento = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(documento.RootElement.TryGetProperty("paths", out var paths));
        Assert.True(paths.TryGetProperty("/v1/solicitudes-alta", out var ruta));

        var operaciones = ruta.EnumerateObject().Select(p => p.Name).ToList();
        Assert.Equal(["post"], operaciones);

        foreach (var path in paths.EnumerateObject())
        {
            if (path.Name.Equals("/v1/solicitudes-alta", StringComparison.Ordinal))
            {
                continue;
            }

            Assert.False(
                path.Name.StartsWith("/v1/solicitudes-alta/", StringComparison.Ordinal),
                $"No debe existir el endpoint público {path.Name}.");
        }
    }

    private static async Task<HttpResponseMessage> PostAsync(
        HttpClient client,
        SolicitudAltaRequest request,
        string ip,
        string path = "/v1/solicitudes-alta")
    {
        using var mensaje = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(request)
        };
        mensaje.Headers.Add("X-Forwarded-For", ip);

        return await client.SendAsync(mensaje);
    }

    private static async Task<HttpResponseMessage> PostRawAsync(
        HttpClient client,
        string cuerpo,
        string? ip = null)
    {
        using var mensaje = new HttpRequestMessage(HttpMethod.Post, "/v1/solicitudes-alta")
        {
            Content = new StringContent(cuerpo, Encoding.UTF8, "application/json")
        };

        if (ip is not null)
        {
            mensaje.Headers.Add("X-Forwarded-For", ip);
        }

        return await client.SendAsync(mensaje);
    }

    private static MongoConnectionException MongoNoDisponible()
        => new(
            new ConnectionId(new ServerId(new ClusterId(1), new DnsEndPoint("localhost", 27017))),
            "sin conexión de prueba.");

    private static string CuerpoConPadding(string email, string padding) =>
        $$"""
        {
          "razonSocial": "ACME S.A.",
          "cuit": "{{CuitValido}}",
          "condicionIva": 1,
          "nombreSolicitante": "Ana Pérez",
          "email": "{{email}}",
          "telefono": "+54 11 5555-4444",
          "domicilio": "Av. Corrientes 1234",
          "ciudad": "Buenos Aires",
          "provincia": "CABA",
          "codigoPostal": "C1043",
          "mensaje": null,
          "aceptaTerminos": true,
          "padding": "{{padding}}"
        }
        """;

    private static string CuerpoConEmailDuplicado(string senuelo, string victima) =>
        $$"""
        {
          "razonSocial": "ACME S.A.",
          "cuit": "{{CuitValido}}",
          "condicionIva": 1,
          "nombreSolicitante": "Ana Pérez",
          "email": "{{senuelo}}",
          "telefono": "+54 11 5555-4444",
          "domicilio": "Av. Corrientes 1234",
          "ciudad": "Buenos Aires",
          "provincia": "CABA",
          "codigoPostal": "C1043",
          "mensaje": null,
          "aceptaTerminos": true,
          "email": "{{victima}}"
        }
        """;

    private static string IpUnica()
        => $"198.51.100.{Interlocked.Increment(ref s_ip) % 254 + 1}";

    private static SolicitudAltaRequest Request() => new(
        RazonSocial: "ACME S.A.",
        Cuit: CuitValido,
        CondicionIva: (int)CondicionIva.ResponsableInscripto,
        NombreSolicitante: "Ana Pérez",
        Email: EmailValido,
        Telefono: "+54 11 5555-4444",
        Domicilio: "Av. Corrientes 1234",
        Ciudad: "Buenos Aires",
        Provincia: "CABA",
        CodigoPostal: "C1043",
        Mensaje: null,
        AceptaTerminos: true);

    private sealed record ErrorResponse(string Codigo, string Mensaje);
}
