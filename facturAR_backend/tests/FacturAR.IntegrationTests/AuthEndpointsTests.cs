using System.Net;
using System.Net.Http.Json;
using FacturAR.Contracts.Auth;
using FacturAR.Contracts.Comunes;
using FacturAR.Domain.Entities;
using FacturAR.Domain.Enums;
using FacturAR.Infrastructure.Mongo;
using FacturAR.Infrastructure.Password;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace FacturAR.IntegrationTests;

public sealed class AuthEndpointsTests : IAsyncLifetime
{
    private const string EmpresaEmail = "admin@demo.com";
    private const string EmpresaPassword = "Secreta123!";
    private const string AdminEmail = "admin@facturar.ar";
    private const string AdminPassword = "SuperSecreta123!";
    private const string CookieEmpresa = ".FacturAR.Contribuyente";
    private const string CookieAdmin = ".FacturAR.Admin";

    private readonly string _databaseName = $"facturar_it_{Guid.NewGuid():N}"[..28];
    private readonly PasswordHasher _hasher = new(1000);

    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;
    private IMongoClient? _mongoClient;

    public async Task InitializeAsync()
    {
        if (string.IsNullOrWhiteSpace(MongoTestSettings.ConnectionString))
        {
            return;
        }

        _factory = CrearFactory(new Dictionary<string, string>());
        _client = CrearCliente(_factory);
        _mongoClient = _factory.Services.GetRequiredService<IMongoClient>();

        await SembrarAsync();
    }

    [MongoFact]
    public async Task LoginDeEmpresa_ConCredencialesValidas_DevuelveSesionYCookieLax()
    {
        var response = await Client.PostAsJsonAsync(
            "/v1/auth/login",
            new LoginRequest(EmpresaEmail, EmpresaPassword));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var setCookie = SetCookie(response);
        Assert.Contains(CookieEmpresa, setCookie);
        Assert.Contains("httponly", setCookie.Replace(" ", string.Empty), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", setCookie.Replace(" ", string.Empty), StringComparison.OrdinalIgnoreCase);

        var payload = await response.Content.ReadFromJsonAsync<SesionEmpresaResponse>();
        Assert.NotNull(payload);
        Assert.Equal(EmpresaEmail, payload.Email);
        Assert.Equal("Administrador", payload.Rol);
        Assert.Equal("Demo SRL", payload.ContribuyenteRazonSocial);
        Assert.True(payload.ExpiraAtUtc > DateTime.UtcNow);
        Assert.InRange(
            payload.ExpiraAtUtc,
            DateTime.UtcNow.AddHours(8).AddMinutes(-2),
            DateTime.UtcNow.AddHours(8).AddMinutes(2));

        var me = await GetConCookieAsync("/v1/auth/me", CookieDe(setCookie));
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);

        var sesion = await me.Content.ReadFromJsonAsync<SesionEmpresaResponse>();
        Assert.NotNull(sesion);
        Assert.Equal(payload.UsuarioId, sesion.UsuarioId);
        Assert.Equal(payload.ContribuyenteId, sesion.ContribuyenteId);
    }

    [MongoFact]
    public async Task LoginDeEmpresa_ConPasswordIncorrecta_DevuelveCredencialesInvalidasSinCookie()
    {
        var response = await Client.PostAsJsonAsync(
            "/v1/auth/login",
            new LoginRequest(EmpresaEmail, "OtraClave123!"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("CREDENCIALES_INVALIDAS", error.Codigo);
    }

    [MongoFact]
    public async Task LoginDeEmpresa_ConUsuarioInactivo_DevuelveCredencialesInvalidas()
    {
        await GetUsuarios().UpdateOneAsync(
            u => u.Email == EmpresaEmail,
            Builders<Usuario>.Update.Set(u => u.Activo, false));

        var response = await Client.PostAsJsonAsync(
            "/v1/auth/login",
            new LoginRequest(EmpresaEmail, EmpresaPassword));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [MongoFact]
    public async Task LoginDeEmpresa_ConContribuyenteInactivo_DevuelveCredencialesInvalidas()
    {
        await GetContribuyentes().UpdateOneAsync(
            _ => true,
            Builders<Contribuyente>.Update.Set(c => c.Activo, false));

        var response = await Client.PostAsJsonAsync(
            "/v1/auth/login",
            new LoginRequest(EmpresaEmail, EmpresaPassword));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [MongoFact]
    public async Task LoginGlobal_ConCredencialesValidas_DevuelveSesionYCookieStrict()
    {
        var response = await Client.PostAsJsonAsync(
            "/v1/admin/auth/login",
            new LoginRequest(AdminEmail, AdminPassword));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var setCookie = SetCookie(response);
        Assert.Contains(CookieAdmin, setCookie);
        Assert.DoesNotContain(CookieEmpresa, setCookie);
        Assert.Contains("httponly", setCookie.Replace(" ", string.Empty), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", setCookie.Replace(" ", string.Empty), StringComparison.OrdinalIgnoreCase);

        var payload = await response.Content.ReadFromJsonAsync<SesionAdminResponse>();
        Assert.NotNull(payload);
        Assert.Equal("SuperAdmin", payload.RolGlobal);
        Assert.Equal("Administración FacturAR", payload.Nombre);
        Assert.InRange(
            payload.ExpiraAtUtc,
            DateTime.UtcNow.AddHours(2).AddMinutes(-2),
            DateTime.UtcNow.AddHours(2).AddMinutes(2));

        var me = await GetConCookieAsync("/v1/admin/auth/me", CookieDe(setCookie));
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);

        var sesion = await me.Content.ReadFromJsonAsync<SesionAdminResponse>();
        Assert.NotNull(sesion);
        Assert.Equal(payload.AdministradorId, sesion.AdministradorId);
        Assert.Equal("SuperAdmin", sesion.RolGlobal);
    }

    [MongoFact]
    public async Task UnUsuarioDeEmpresa_NoPuedeAccederALaConsolaGlobal()
    {
        var cookie = await LoginEmpresaAsync();

        var me = await GetConCookieAsync("/v1/admin/auth/me", cookie);

        Assert.Equal(HttpStatusCode.Forbidden, me.StatusCode);

        var error = await me.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("SIN_PERMISOS", error.Codigo);
    }

    [MongoFact]
    public async Task UnAdministradorGlobal_NoPuedeUsarElTenantDeEmpresa()
    {
        var cookie = await LoginAdminAsync();

        var me = await GetConCookieAsync("/v1/auth/me", cookie);

        Assert.Equal(HttpStatusCode.Forbidden, me.StatusCode);
    }

    [MongoFact]
    public async Task LogoutDeEmpresa_Devuelve204YBorraLaCookie()
    {
        var cookie = await LoginEmpresaAsync();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/auth/logout");
        request.Headers.TryAddWithoutValidation("Cookie", cookie);

        var response = await Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var setCookie = SetCookie(response);
        Assert.Contains(CookieEmpresa, setCookie);
        Assert.Contains("1970", setCookie);
    }

    [MongoFact]
    public async Task LoginYLogout_RegistranLosEventosDeAuditoria()
    {
        var cookie = await LoginEmpresaAsync();

        await Client.PostAsJsonAsync("/v1/auth/login", new LoginRequest(EmpresaEmail, "Incorrecta123!"));
        await GetConCookieAsync("/v1/admin/auth/me", cookie);

        using var logout = new HttpRequestMessage(HttpMethod.Post, "/v1/auth/logout");
        logout.Headers.TryAddWithoutValidation("Cookie", cookie);
        await Client.SendAsync(logout);

        var acciones = await GetEventos()
            .Find(_ => true)
            .Project(e => e.Accion)
            .ToListAsync();

        Assert.Contains("LoginEmpresa", acciones);
        Assert.Contains("LoginFallido", acciones);
        Assert.Contains("AccesoDenegado", acciones);
        Assert.Contains("LogoutEmpresa", acciones);
    }

    [MongoFact]
    public async Task Login_SinSesion_Devuelve401EnMe()
    {
        var response = await GetConCookieAsync("/v1/auth/me", cookie: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("CREDENCIALES_INVALIDAS", error.Codigo);
    }

    [MongoFact]
    public async Task Login_ConBodyInvalido_DevuelveValidacion()
    {
        var response = await Client.PostAsJsonAsync(
            "/v1/auth/login",
            new LoginRequest(string.Empty, EmpresaPassword));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("VALIDACION", error.Codigo);
    }

    [MongoFact]
    public async Task Login_AlSuperarElRateLimitPorIp_Devuelve429()
    {
        using var factory = CrearFactory(new Dictionary<string, string>
        {
            ["Auth:Login:RateLimitPorIp"] = "1"
        });
        using var client = CrearCliente(factory);

        var primero = await client.PostAsJsonAsync(
            "/v1/auth/login",
            new LoginRequest(EmpresaEmail, EmpresaPassword));
        var segundo = await client.PostAsJsonAsync(
            "/v1/auth/login",
            new LoginRequest(EmpresaEmail, EmpresaPassword));

        Assert.NotEqual(HttpStatusCode.TooManyRequests, primero.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, segundo.StatusCode);

        var error = await segundo.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("RATE_LIMIT", error.Codigo);
    }

    [MongoFact]
    public async Task Login_AlAlcanzarLosIntentosFallidos_BloqueaAunqueLaPasswordSeaCorrecta()
    {
        using var factory = CrearFactory(new Dictionary<string, string>
        {
            ["Auth:Login:MaxIntentosFallidos"] = "2",
            ["Auth:Login:MinutosBloqueo"] = "15"
        });
        using var client = CrearCliente(factory);

        await client.PostAsJsonAsync("/v1/auth/login", new LoginRequest(EmpresaEmail, "Incorrecta123!"));
        await client.PostAsJsonAsync("/v1/auth/login", new LoginRequest(EmpresaEmail, "Incorrecta123!"));

        var correcta = await client.PostAsJsonAsync(
            "/v1/auth/login",
            new LoginRequest(EmpresaEmail, EmpresaPassword));

        Assert.Equal(HttpStatusCode.Unauthorized, correcta.StatusCode);
    }

    [MongoFact]
    public async Task LoginExitoso_NoExponeLaPasswordNiElHash()
    {
        var hash = (await GetUsuarios().Find(u => u.Email == EmpresaEmail).FirstAsync()).PasswordHash;

        var response = await Client.PostAsJsonAsync(
            "/v1/auth/login",
            new LoginRequest(EmpresaEmail, EmpresaPassword));

        var body = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("password", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(hash, body, StringComparison.Ordinal);
        Assert.DoesNotContain(EmpresaPassword, body, StringComparison.Ordinal);
    }

    public Task DisposeAsync()
    {
        if (_mongoClient is not null)
        {
            _mongoClient.DropDatabase(_databaseName);
        }

        _client?.Dispose();
        _factory?.Dispose();

        return Task.CompletedTask;
    }

    private WebApplicationFactory<Program> CrearFactory(Dictionary<string, string> extra)
    {
        var settings = new Dictionary<string, string>
        {
            ["Mongo:ConnectionString"] = MongoTestSettings.GetConnectionStringWithTimeout(),
            ["Mongo:DatabaseName"] = _databaseName,
            ["Mongo:InitializeIndexes"] = "true",
            ["Auth:Login:RateLimitPorIp"] = "1000",
            ["BootstrapAdmin:Email"] = string.Empty
        };

        foreach (var (clave, valor) in extra)
        {
            settings[clave] = valor;
        }

        return new ConfiguredWebApplicationFactory(settings);
    }

    private static HttpClient CrearCliente(WebApplicationFactory<Program> factory)
        => factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false,
            AllowAutoRedirect = false
        });

    private async Task SembrarAsync()
    {
        var contribuyente = new Contribuyente
        {
            RazonSocial = "Demo SRL",
            Cuit = "20123456786",
            Activo = true
        };

        await GetContribuyentes().InsertOneAsync(contribuyente);

        await GetUsuarios().InsertOneAsync(new Usuario
        {
            ContribuyenteId = contribuyente.Id,
            Email = EmpresaEmail,
            PasswordHash = _hasher.Hash(EmpresaPassword),
            Rol = RolUsuario.Administrador,
            Activo = true
        });

        await GetAdministradores().InsertOneAsync(new AdministradorFacturar
        {
            Nombre = "Administración FacturAR",
            Email = AdminEmail,
            PasswordHash = _hasher.Hash(AdminPassword),
            Rol = RolGlobal.SuperAdmin,
            Activo = true
        });
    }

    private async Task<string> LoginEmpresaAsync()
    {
        var response = await Client.PostAsJsonAsync(
            "/v1/auth/login",
            new LoginRequest(EmpresaEmail, EmpresaPassword));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return CookieDe(SetCookie(response));
    }

    private async Task<string> LoginAdminAsync()
    {
        var response = await Client.PostAsJsonAsync(
            "/v1/admin/auth/login",
            new LoginRequest(AdminEmail, AdminPassword));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return CookieDe(SetCookie(response));
    }

    private async Task<HttpResponseMessage> GetConCookieAsync(string ruta, string? cookie)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, ruta);
        if (!string.IsNullOrWhiteSpace(cookie))
        {
            request.Headers.TryAddWithoutValidation("Cookie", cookie);
        }

        return await Client.SendAsync(request);
    }

    private static string SetCookie(HttpResponseMessage response)
        => response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.First()
            : throw new InvalidOperationException("La respuesta no incluye Set-Cookie.");

    private static string CookieDe(string setCookie) => setCookie.Split(';')[0];

    private HttpClient Client => _client
        ?? throw new InvalidOperationException("El test de MongoDB no fue inicializado.");

    private IMongoDatabase Database => (_mongoClient
        ?? throw new InvalidOperationException("El test de MongoDB no fue inicializado."))
        .GetDatabase(_databaseName);

    private IMongoCollection<Usuario> GetUsuarios()
        => Database.GetCollection<Usuario>(MongoCollections.Usuarios);

    private IMongoCollection<Contribuyente> GetContribuyentes()
        => Database.GetCollection<Contribuyente>(MongoCollections.Contribuyentes);

    private IMongoCollection<AdministradorFacturar> GetAdministradores()
        => Database.GetCollection<AdministradorFacturar>(MongoCollections.Administradores);

    private IMongoCollection<EventoAuditoria> GetEventos()
        => Database.GetCollection<EventoAuditoria>(MongoCollections.Eventos);
}