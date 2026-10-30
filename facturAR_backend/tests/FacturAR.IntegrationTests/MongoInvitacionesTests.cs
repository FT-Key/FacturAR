using System.Net;
using System.Net.Http.Json;
using FacturAR.Application.Notificaciones;
using FacturAR.Contracts.Auth;
using FacturAR.Contracts.Comunes;
using FacturAR.Contracts.Invitaciones;
using FacturAR.Domain.Entities;
using FacturAR.Domain.Enums;
using FacturAR.Domain.ValueObjects;
using FacturAR.Infrastructure.Mongo;
using FacturAR.Infrastructure.Password;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MongoDB.Driver;

namespace FacturAR.IntegrationTests;

public sealed class MongoInvitacionesTests : IAsyncLifetime
{
    private const string EmailInvitado = "solicitante@demo.com";
    private const string SuperAdminEmail = "super@facturar.ar";
    private const string AdminPassword = "SuperSecreta123!";

    private static readonly string PasswordValida = ContrasenaDePrueba();

    private readonly string _databaseName = $"facturar_it_{Guid.NewGuid():N}"[..28];
    private readonly FakeEmailSender _emails = new();

    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;
    private IMongoClient? _mongoClient;
    private Contribuyente? _contribuyente;

    public Task InitializeAsync() => IniciarAsync(activacionesPorIp: 1000);

    public Task DisposeAsync()
    {
        _mongoClient?.DropDatabase(_databaseName);
        _client?.Dispose();
        _factory?.Dispose();
        return Task.CompletedTask;
    }

    [MongoFact]
    public async Task Validar_TokenVigente_DevuelveSoloRazonSocialEmailEnmascaradoYVencimiento()
    {
        var (token, invitacion) = await SembrarInvitacionAsync();

        var response = await _client!.GetAsync($"/v1/invitaciones/{token}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        var validacion = await response.Content.ReadFromJsonAsync<InvitacionValidacionResponse>();
        Assert.Equal("Demo SRL", validacion!.RazonSocial);
        Assert.Equal("s***@demo.com", validacion.EmailEnmascarado);
        Assert.Equal(invitacion.ExpiraAt, validacion.ExpiraAt, TimeSpan.FromMilliseconds(5));
        Assert.DoesNotContain(EmailInvitado, body);
        Assert.DoesNotContain(token, body);
        Assert.DoesNotContain(invitacion.TokenPrefix, body);
        Assert.DoesNotContain(_contribuyente!.Id.ToString(), body);
        Assert.DoesNotContain(invitacion.Id.ToString(), body);
    }

    [MongoFact]
    public async Task TokenInexistenteYRevocado_DevuelvenElMismo404EnGetYPost()
    {
        var (revocado, invitacion) = await SembrarInvitacionAsync();
        await Invitaciones().UpdateOneAsync(
            i => i.Id == invitacion.Id,
            Builders<Invitacion>.Update.Set(i => i.Estado, EstadoInvitacion.Revocada));
        var inexistente = TokenInvitacion.Generar(DateTime.UtcNow).Token;

        var errores = new List<ErrorResponse>();
        foreach (var token in new[] { inexistente, revocado })
        {
            var get = await _client!.GetAsync($"/v1/invitaciones/{token}");
            var post = await AceptarAsync(token, PasswordValida);
            errores.Add(await AssertErrorAsync(get, HttpStatusCode.NotFound, "INVITACION_NO_ENCONTRADA"));
            errores.Add(await AssertErrorAsync(post, HttpStatusCode.NotFound, "INVITACION_NO_ENCONTRADA"));
        }

        Assert.Single(errores.Select(e => e.Mensaje).Distinct());
        Assert.Equal(0, await Usuarios().CountDocumentsAsync(FilterDefinition<Usuario>.Empty));
    }

    [MongoFact]
    public async Task TokenVencido_Devuelve410SinCrearUsuario()
    {
        var (token, _) = await SembrarInvitacionAsync(expiraAt: DateTime.UtcNow.AddMinutes(-1));

        await AssertErrorAsync(await _client!.GetAsync($"/v1/invitaciones/{token}"), HttpStatusCode.Gone, "INVITACION_VENCIDA");
        await AssertErrorAsync(await AceptarAsync(token, PasswordValida), HttpStatusCode.Gone, "INVITACION_VENCIDA");
        Assert.Equal(0, await Usuarios().CountDocumentsAsync(FilterDefinition<Usuario>.Empty));
    }

    [MongoFact]
    public async Task Aceptar_PasswordDebilOConfirmacionDistinta_NoCreaUsuarioYLaInvitacionSigueVigente()
    {
        var (token, invitacion) = await SembrarInvitacionAsync();

        await AssertErrorAsync(await AceptarAsync(token, "123456"), HttpStatusCode.BadRequest, "PASSWORD_DEBIL");
        await AssertErrorAsync(await AceptarAsync(token, "1234567890"), HttpStatusCode.BadRequest, "PASSWORD_DEBIL");
        await AssertErrorAsync(await AceptarAsync(token, "abcdefghij"), HttpStatusCode.BadRequest, "PASSWORD_DEBIL");
        await AssertErrorAsync(await AceptarAsync(token, "contrasena1"), HttpStatusCode.BadRequest, "PASSWORD_DEBIL");
        var distinta = await AssertErrorAsync(
            await AceptarAsync(token, PasswordValida, PasswordValida + "x"),
            HttpStatusCode.BadRequest,
            "VALIDACION");

        Assert.Equal("Las contraseñas no coinciden.", distinta.Mensaje);
        Assert.Equal(0, await Usuarios().CountDocumentsAsync(FilterDefinition<Usuario>.Empty));
        var guardada = await Invitaciones().Find(i => i.Id == invitacion.Id).SingleAsync();
        Assert.Equal(EstadoInvitacion.Pendiente, guardada.Estado);
    }

    [MongoFact]
    public async Task Aceptar_TokenVigente_CreaAdministradorMarcaUsadaYNoIniciaSesion()
    {
        var (token, invitacion) = await SembrarInvitacionAsync();

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/v1/invitaciones/{token}/aceptar")
        {
            Content = JsonContent.Create(new
            {
                password = PasswordValida,
                confirmacionPassword = PasswordValida,
                email = "atacante@otra.com",
                contribuyenteId = Guid.NewGuid(),
                rol = "Consulta"
            })
        };
        var response = await _client!.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
        var respuesta = await response.Content.ReadFromJsonAsync<AceptarInvitacionResponse>();
        Assert.Equal("Cuenta activada. Ya podés iniciar sesión.", respuesta!.Mensaje);
        Assert.DoesNotContain(PasswordValida, await response.Content.ReadAsStringAsync());

        var usuario = await Usuarios().Find(FilterDefinition<Usuario>.Empty).SingleAsync();
        Assert.Equal(_contribuyente!.Id, usuario.ContribuyenteId);
        Assert.Equal(EmailInvitado, usuario.Email);
        Assert.Equal(RolUsuario.Administrador, usuario.Rol);
        Assert.True(usuario.Activo);
        Assert.NotNull(usuario.EmailVerificadoAt);
        Assert.NotEqual(PasswordValida, usuario.PasswordHash);
        Assert.True(new PasswordHasher(1000).Verify(PasswordValida, usuario.PasswordHash));

        var usada = await Invitaciones().Find(i => i.Id == invitacion.Id).SingleAsync();
        Assert.Equal(EstadoInvitacion.Usada, usada.Estado);
        Assert.NotNull(usada.UsadaAt);

        var evento = await Coleccion<EventoAuditoria>(MongoCollections.Eventos)
            .Find(e => e.Accion == "cuenta.activada").SingleAsync();
        Assert.Equal(usuario.Id, evento.UsuarioId);
        Assert.Equal(_contribuyente.Id, evento.ContribuyenteId);

        await AssertErrorAsync(await AceptarAsync(token, PasswordValida), HttpStatusCode.Conflict, "INVITACION_USADA");
        await AssertErrorAsync(await _client.GetAsync($"/v1/invitaciones/{token}"), HttpStatusCode.Conflict, "INVITACION_USADA");
        Assert.Equal(1, await Usuarios().CountDocumentsAsync(FilterDefinition<Usuario>.Empty));
    }

    [MongoFact]
    public async Task Aceptar_Concurrente_CreaUnSoloUsuario()
    {
        var (token, _) = await SembrarInvitacionAsync();

        var respuestas = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => AceptarAsync(token, PasswordValida)));

        Assert.Single(respuestas, r => r.StatusCode == HttpStatusCode.OK);
        Assert.All(
            respuestas.Where(r => r.StatusCode != HttpStatusCode.OK),
            r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
        Assert.Equal(1, await Usuarios().CountDocumentsAsync(FilterDefinition<Usuario>.Empty));
        Assert.Equal(1, await Invitaciones().CountDocumentsAsync(i => i.Estado == EstadoInvitacion.Usada));
    }

    [MongoFact]
    public async Task Aceptar_UsuarioYaExistenteEnLaEmpresa_Devuelve409SinDuplicarNiUsarLaInvitacion()
    {
        var (token, invitacion) = await SembrarInvitacionAsync();
        var existente = new Usuario
        {
            ContribuyenteId = _contribuyente!.Id,
            Email = EmailInvitado,
            PasswordHash = "hash-original",
            Rol = RolUsuario.Emisor,
            Activo = true
        };
        await Usuarios().InsertOneAsync(existente);

        await AssertErrorAsync(await AceptarAsync(token, PasswordValida), HttpStatusCode.Conflict, "INVITACION_USADA");

        var usuario = await Usuarios().Find(FilterDefinition<Usuario>.Empty).SingleAsync();
        Assert.Equal("hash-original", usuario.PasswordHash);
        Assert.Equal(RolUsuario.Emisor, usuario.Rol);
        Assert.Equal(EstadoInvitacion.Pendiente, (await Invitaciones().Find(i => i.Id == invitacion.Id).SingleAsync()).Estado);
    }

    [MongoFact]
    public async Task EmpresaInactiva_Devuelve403YLaInvitacionSigueVigente()
    {
        var (token, invitacion) = await SembrarInvitacionAsync();
        await Coleccion<Contribuyente>(MongoCollections.Contribuyentes).UpdateOneAsync(
            c => c.Id == _contribuyente!.Id,
            Builders<Contribuyente>.Update.Set(c => c.Activo, false));

        await AssertErrorAsync(await _client!.GetAsync($"/v1/invitaciones/{token}"), HttpStatusCode.Forbidden, "CUENTA_INACTIVA");
        await AssertErrorAsync(await AceptarAsync(token, PasswordValida), HttpStatusCode.Forbidden, "CUENTA_INACTIVA");

        Assert.Equal(0, await Usuarios().CountDocumentsAsync(FilterDefinition<Usuario>.Empty));
        Assert.Equal(EstadoInvitacion.Pendiente, (await Invitaciones().Find(i => i.Id == invitacion.Id).SingleAsync()).Estado);
    }

    [MongoFact]
    public async Task FlujoCompleto_AprobarActivarEIniciarSesion()
    {
        var solicitud = new SolicitudAltaEmpresa
        {
            RazonSocial = "Flujo Completo SA",
            Cuit = "30712345671",
            CondicionIva = CondicionIva.ResponsableInscripto,
            NombreSolicitante = "Ana Pérez",
            EmailSolicitante = "flujo@empresa.com",
            Telefono = "3815551234",
            Domicilio = "San Martín 100",
            Ciudad = "Tucumán",
            Provincia = "Tucumán",
            CodigoPostal = "4000",
            AceptaTerminos = true
        };
        await Coleccion<SolicitudAltaEmpresa>(MongoCollections.SolicitudesAlta).InsertOneAsync(solicitud);

        var login = await _client!.PostAsJsonAsync("/v1/admin/auth/login", new LoginRequest(SuperAdminEmail, AdminPassword));
        var cookieAdmin = login.Headers.GetValues("Set-Cookie").First().Split(';')[0];
        using (var aprobar = new HttpRequestMessage(HttpMethod.Post, $"/v1/admin/solicitudes/{solicitud.Id}/aprobar"))
        {
            aprobar.Headers.TryAddWithoutValidation("Cookie", cookieAdmin);
            Assert.Equal(HttpStatusCode.OK, (await _client!.SendAsync(aprobar)).StatusCode);
        }

        var email = Assert.Single(_emails.Enviados, e => e.Plantilla == PlantillasEmail.NombreSolicitudAprobada);
        var link = email.CuerpoTexto.Split('\n').Select(l => l.Trim()).Single(l => l.Contains("/activar?token=", StringComparison.Ordinal));
        Assert.StartsWith("https://portal.test/activar?token=", link);
        var token = Uri.UnescapeDataString(link.Split("token=")[1]);

        var validacion = await _client.GetFromJsonAsync<InvitacionValidacionResponse>($"/v1/invitaciones/{token}");
        Assert.Equal("Flujo Completo SA", validacion!.RazonSocial);
        Assert.Equal("f***@empresa.com", validacion.EmailEnmascarado);

        Assert.Equal(HttpStatusCode.OK, (await AceptarAsync(token, PasswordValida)).StatusCode);

        var sesion = await _client.PostAsJsonAsync("/v1/auth/login", new LoginRequest("flujo@empresa.com", PasswordValida));
        Assert.Equal(HttpStatusCode.OK, sesion.StatusCode);
        var datos = await sesion.Content.ReadFromJsonAsync<SesionEmpresaResponse>();
        Assert.Equal("Administrador", datos!.Rol);
        Assert.Equal("Flujo Completo SA", datos.ContribuyenteRazonSocial);
    }

    [MongoFact]
    public async Task Aceptar_SuperaElRateLimit_Devuelve429ConRetryAfter()
    {
        await DisposeAsync();
        await IniciarAsync(activacionesPorIp: 1);
        var (token, _) = await SembrarInvitacionAsync();

        await AceptarAsync(token, "123456");
        var bloqueada = await AceptarAsync(token, PasswordValida);

        await AssertErrorAsync(bloqueada, HttpStatusCode.TooManyRequests, "RATE_LIMIT");
        Assert.True(bloqueada.Headers.Contains("Retry-After"));
        Assert.Equal(0, await Usuarios().CountDocumentsAsync(FilterDefinition<Usuario>.Empty));
    }

    private async Task IniciarAsync(int activacionesPorIp)
    {
        if (string.IsNullOrWhiteSpace(MongoTestSettings.ConnectionString))
        {
            return;
        }

        _emails.Enviados.Clear();
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.UseSetting("Mongo:ConnectionString", MongoTestSettings.GetConnectionStringWithTimeout());
                builder.UseSetting("Mongo:DatabaseName", _databaseName);
                builder.UseSetting("Mongo:InitializeIndexes", "true");
                builder.UseSetting("Auth:Login:RateLimitPorIp", "1000");
                builder.UseSetting("Auth:PasswordHashIterations", "1000");
                builder.UseSetting("BootstrapAdmin:Email", string.Empty);
                builder.UseSetting("Admin:NotificationEmail", string.Empty);
                builder.UseSetting("Portal:BaseUrl", "https://portal.test");
                builder.UseSetting("RateLimit:Invitacion:ValidacionesPorIp", "1000");
                builder.UseSetting("RateLimit:Invitacion:ActivacionesPorIp", activacionesPorIp.ToString());
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IEmailSender>();
                    services.AddSingleton<IEmailSender>(_emails);
                });
            });

        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false,
            AllowAutoRedirect = false
        });
        _mongoClient = _factory.Services.GetRequiredService<IMongoClient>();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await MongoIndexInitializer.InitializeAsync(_factory.Services.GetRequiredService<MongoDbContext>(), timeout.Token);

        _contribuyente = new Contribuyente { RazonSocial = "Demo SRL", Cuit = "20123456786", Activo = true };
        await Coleccion<Contribuyente>(MongoCollections.Contribuyentes).InsertOneAsync(_contribuyente);

        await Coleccion<AdministradorFacturar>(MongoCollections.Administradores).InsertOneAsync(new AdministradorFacturar
        {
            Nombre = "SuperAdmin",
            Email = SuperAdminEmail,
            PasswordHash = new PasswordHasher(1000).Hash(AdminPassword),
            Rol = RolGlobal.SuperAdmin,
            Activo = true
        });
    }

    private async Task<(string Token, Invitacion Invitacion)> SembrarInvitacionAsync(DateTime? expiraAt = null)
    {
        var token = TokenInvitacion.Generar(DateTime.UtcNow);
        var invitacion = new Invitacion
        {
            ContribuyenteId = _contribuyente!.Id,
            SolicitudId = Guid.NewGuid(),
            Email = EmailInvitado,
            Rol = RolUsuario.Administrador,
            TokenHash = token.Hash,
            TokenPrefix = token.Prefix,
            Estado = EstadoInvitacion.Pendiente,
            ExpiraAt = expiraAt ?? token.ExpiraAt,
            CreadaPorAdministradorId = Guid.NewGuid()
        };
        await Invitaciones().InsertOneAsync(invitacion);
        return (token.Token, invitacion);
    }

    private Task<HttpResponseMessage> AceptarAsync(string token, string password, string? confirmacion = null)
        => _client!.PostAsJsonAsync(
            $"/v1/invitaciones/{token}/aceptar",
            new AceptarInvitacionRequest(password, confirmacion ?? password));

    private static async Task<ErrorResponse> AssertErrorAsync(HttpResponseMessage response, HttpStatusCode status, string codigo)
    {
        Assert.Equal(status, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal(codigo, error!.Codigo);
        return error;
    }

    private static string ContrasenaDePrueba()
        => $"Prueba{Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(4))}z7";

    private IMongoCollection<Invitacion> Invitaciones() => Coleccion<Invitacion>(MongoCollections.Invitaciones);

    private IMongoCollection<Usuario> Usuarios() => Coleccion<Usuario>(MongoCollections.Usuarios);

    private IMongoCollection<T> Coleccion<T>(string nombre)
        => _mongoClient!.GetDatabase(_databaseName).GetCollection<T>(nombre);
}
