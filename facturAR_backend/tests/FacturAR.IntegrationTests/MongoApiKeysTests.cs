using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.RegularExpressions;
using FacturAR.Application.ApiKeys;
using FacturAR.Contracts.ApiKeys;
using FacturAR.Contracts.Auth;
using FacturAR.Contracts.Comunes;
using FacturAR.Domain.Entities;
using FacturAR.Domain.Enums;
using FacturAR.Infrastructure.Mongo;
using FacturAR.Infrastructure.Password;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;

namespace FacturAR.IntegrationTests;

public sealed class MongoApiKeysTests : IAsyncLifetime
{
    private const string EmailAdminA = "admin@demo.com";
    private const string EmailConsultaA = "consulta@demo.com";
    private const string EmailAdminB = "admin@otra.com";
    private const string SuperAdminEmail = "super@facturar.ar";
    private const string EmpresaPassword = "Secreta123!";
    private const string SuperAdminPassword = "SuperSecreta123!";

    private static readonly Regex FormatoValor = new(
        "^fak_[0-9a-f]{12}_[A-Za-z0-9_-]{43}$",
        RegexOptions.CultureInvariant);

    private static readonly Regex FormatoPrefijo = new(
        "^[0-9a-f]{12}$",
        RegexOptions.CultureInvariant);

    private static readonly Regex FormatoHash = new(
        "^[0-9a-f]{64}$",
        RegexOptions.CultureInvariant);

    private readonly string _databaseName = $"facturar_it_{Guid.NewGuid():N}"[..28];
    private readonly PasswordHasher _hasher = new(1000);
    private readonly ApiKeyHasher _apiKeyHasher = new();

    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;
    private IMongoClient? _mongoClient;
    private Contribuyente? _contribuyenteA;
    private Contribuyente? _contribuyenteB;

    public async Task InitializeAsync()
    {
        if (string.IsNullOrWhiteSpace(MongoTestSettings.ConnectionString))
        {
            return;
        }

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
            });

        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false,
            AllowAutoRedirect = false
        });
        _mongoClient = _factory.Services.GetRequiredService<IMongoClient>();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await MongoIndexInitializer.InitializeAsync(
            _factory.Services.GetRequiredService<MongoDbContext>(),
            timeout.Token);

        _contribuyenteA = new Contribuyente { RazonSocial = "Demo SRL", Cuit = "20123456786", Activo = true };
        _contribuyenteB = new Contribuyente { RazonSocial = "Otra SRL", Cuit = "30712345671", Activo = true };
        await Contribuyentes().InsertManyAsync([_contribuyenteA, _contribuyenteB]);

        await Usuarios().InsertManyAsync(
        [
            NuevoUsuario(_contribuyenteA.Id, EmailAdminA, RolUsuario.Administrador),
            NuevoUsuario(_contribuyenteA.Id, EmailConsultaA, RolUsuario.Consulta),
            NuevoUsuario(_contribuyenteB.Id, EmailAdminB, RolUsuario.Administrador)
        ]);

        await Coleccion<AdministradorFacturar>(MongoCollections.Administradores).InsertOneAsync(
            new AdministradorFacturar
            {
                Nombre = "SuperAdmin",
                Email = SuperAdminEmail,
                PasswordHash = _hasher.Hash(SuperAdminPassword),
                Rol = RolGlobal.SuperAdmin,
                Activo = true
            });
    }

    public Task DisposeAsync()
    {
        _mongoClient?.DropDatabase(_databaseName);
        _client?.Dispose();
        _factory?.Dispose();
        return Task.CompletedTask;
    }

    [MongoFact]
    public async Task CP01_Crear_Devuelve201ConElValorYGuardaSoloPrefijoYHash()
    {
        var cookie = await LoginEmpresaAsync(EmailAdminA);

        var response = await EnviarAsync(HttpMethod.Post, "/v1/api-keys", cookie, new { nombre = "Ecommerce" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var creada = await LeerAsync<ApiKeyCreadaResponse>(response);
        Assert.Matches(FormatoValor, creada.Valor);
        Assert.Equal($"fak_{creada.Valor.Split('_')[1]}", creada.Prefix);
        Assert.Equal("Ecommerce", creada.Nombre);
        Assert.Equal(DateTimeKind.Utc, creada.CreatedAt.Kind);

        var documento = await ApiKeys().Find(a => a.Id == creada.Id).SingleAsync();
        Assert.Equal(_contribuyenteA!.Id, documento.ContribuyenteId);
        Assert.Equal("Ecommerce", documento.Nombre);
        Assert.Matches(FormatoPrefijo, documento.Prefix);
        Assert.Matches(FormatoHash, documento.Hash);
        Assert.Equal($"fak_{documento.Prefix}", creada.Prefix);
        Assert.Equal(_apiKeyHasher.Hash(creada.Valor), documento.Hash);
        Assert.True(documento.Activa);
        Assert.Null(documento.RevocadaAt);
        Assert.Null(documento.RegeneradaAt);

        var secreto = creada.Valor.Split('_')[2];
        var documentoJson = documento.ToBsonDocument().ToJson();
        Assert.DoesNotContain(creada.Valor, documentoJson, StringComparison.Ordinal);
        Assert.DoesNotContain(secreto, documentoJson, StringComparison.Ordinal);

        var listado = await EnviarAsync(HttpMethod.Get, "/v1/api-keys", cookie);
        Assert.Equal(HttpStatusCode.OK, listado.StatusCode);
        var item = Assert.Single((await LeerAsync<ApiKeyListResponse>(listado)).Items);
        Assert.Equal(creada.Prefix, item.Prefix);
        Assert.DoesNotContain(creada.Valor, await listado.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [MongoFact]
    public async Task CP02_Listar_Devuelve200SinHashValorNiSecreto()
    {
        var cookie = await LoginEmpresaAsync(EmailAdminA);
        var primera = await CrearAsync(cookie, "Ecommerce");
        var segunda = await CrearAsync(cookie, "Facturación");

        var response = await EnviarAsync(HttpMethod.Get, "/v1/api-keys", cookie);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("hash", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("valor", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(primera.Valor, body, StringComparison.Ordinal);
        Assert.DoesNotContain(segunda.Valor, body, StringComparison.Ordinal);
        Assert.DoesNotContain(primera.Valor.Split('_')[2], body, StringComparison.Ordinal);

        var listado = await LeerAsync<ApiKeyListResponse>(response);
        Assert.Equal(2, listado.Items.Count);
        Assert.Contains(listado.Items, i => i.Prefix == primera.Prefix);
        Assert.Contains(listado.Items, i => i.Prefix == segunda.Prefix);

        var propiedades = typeof(ApiKeyResumenResponse).GetProperties().Select(p => p.Name).ToList();
        Assert.DoesNotContain("Hash", propiedades);
        Assert.DoesNotContain("Valor", propiedades);
        Assert.DoesNotContain("Secret", propiedades);
    }

    [MongoFact]
    public async Task CP03_ElHashNuncaApareceEnLasRespuestasDeLaApi()
    {
        var cookie = await LoginEmpresaAsync(EmailAdminA);
        var creada = await CrearAsync(cookie, "Ecommerce");
        var hash = (await ApiKeys().Find(a => a.Id == creada.Id).SingleAsync()).Hash;

        var get = await EnviarAsync(HttpMethod.Get, "/v1/api-keys", cookie);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        Assert.DoesNotContain(hash, await get.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var delete = await EnviarAsync(HttpMethod.Delete, $"/v1/api-keys/{creada.Id}", cookie);
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.DoesNotContain(hash, await delete.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var regenerar = await EnviarAsync(HttpMethod.Post, $"/v1/api-keys/{creada.Id}/regenerar", cookie);
        Assert.Equal(HttpStatusCode.OK, regenerar.StatusCode);
        var regeneradaBody = await regenerar.Content.ReadAsStringAsync();
        Assert.DoesNotContain(hash, regeneradaBody, StringComparison.Ordinal);
        Assert.DoesNotContain("hash", regeneradaBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(creada.Valor, regeneradaBody, StringComparison.Ordinal);
    }

    [MongoFact]
    public async Task CP04_ElValorSoloApareceEnLaRespuestaDeCreacion()
    {
        var cookie = await LoginEmpresaAsync(EmailAdminA);
        var creada = await CrearAsync(cookie, "Ecommerce");

        var listado = await EnviarAsync(HttpMethod.Get, "/v1/api-keys", cookie);
        var listadoBody = await listado.Content.ReadAsStringAsync();
        Assert.DoesNotContain(creada.Valor, listadoBody, StringComparison.Ordinal);
        Assert.DoesNotContain(creada.Valor.Split('_')[2], listadoBody, StringComparison.Ordinal);

        var regenerada = await EnviarAsync(HttpMethod.Post, $"/v1/api-keys/{creada.Id}/regenerar", cookie);
        Assert.Equal(HttpStatusCode.OK, regenerada.StatusCode);
        Assert.DoesNotContain(creada.Valor, await regenerada.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var reintento = await EnviarAsync(HttpMethod.Post, "/v1/api-keys", cookie, new { nombre = "Ecommerce" });
        Assert.Equal(HttpStatusCode.Created, reintento.StatusCode);
        var nueva = await LeerAsync<ApiKeyCreadaResponse>(reintento);
        Assert.NotEqual(creada.Valor, nueva.Valor);
        Assert.NotEqual(creada.Id, nueva.Id);
    }

    [MongoFact]
    public async Task CP05_Revocar_Devuelve204YMarcaLaKeyInactivaConRevocadaAt()
    {
        var cookie = await LoginEmpresaAsync(EmailAdminA);
        var creada = await CrearAsync(cookie, "Ecommerce");

        var response = await EnviarAsync(HttpMethod.Delete, $"/v1/api-keys/{creada.Id}", cookie);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var documento = await ApiKeys().Find(a => a.Id == creada.Id).SingleAsync();
        Assert.False(documento.Activa);
        Assert.NotNull(documento.RevocadaAt);
        Assert.Equal(DateTimeKind.Utc, documento.RevocadaAt!.Value.Kind);
    }

    [MongoFact]
    public async Task CP06_Regenerar_DevuelveValorNuevoYConservaIdYNombre()
    {
        var cookie = await LoginEmpresaAsync(EmailAdminA);
        var creada = await CrearAsync(cookie, "Ecommerce");
        var anterior = await ApiKeys().Find(a => a.Id == creada.Id).SingleAsync();

        var response = await EnviarAsync(HttpMethod.Post, $"/v1/api-keys/{creada.Id}/regenerar", cookie);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var regenerada = await LeerAsync<ApiKeyRegeneradaResponse>(response);
        Assert.Matches(FormatoValor, regenerada.Valor);
        Assert.NotEqual(creada.Valor, regenerada.Valor);
        Assert.Equal(creada.Id, regenerada.Id);
        Assert.Equal("Ecommerce", regenerada.Nombre);
        Assert.Equal(DateTimeKind.Utc, regenerada.RegeneradaAt.Kind);

        var documento = await ApiKeys().Find(a => a.Id == creada.Id).SingleAsync();
        Assert.Equal(anterior.Id, documento.Id);
        Assert.Equal("Ecommerce", documento.Nombre);
        Assert.True(documento.Activa);
        Assert.Null(documento.RevocadaAt);
        Assert.NotNull(documento.RegeneradaAt);
        Assert.NotEqual(anterior.Hash, documento.Hash);
        Assert.Equal(_apiKeyHasher.Hash(regenerada.Valor), documento.Hash);
        Assert.Equal($"fak_{documento.Prefix}", regenerada.Prefix);

        var listado = await EnviarAsync(HttpMethod.Get, "/v1/api-keys", cookie);
        var listadoBody = await listado.Content.ReadAsStringAsync();
        Assert.DoesNotContain(regenerada.Valor, listadoBody, StringComparison.Ordinal);
        Assert.DoesNotContain(documento.Hash, listadoBody, StringComparison.Ordinal);
    }

    [MongoFact]
    public async Task CP07_RolConsulta_Devuelve403SinPermisosYNoCreaLaKey()
    {
        var cookie = await LoginEmpresaAsync(EmailConsultaA);

        var response = await EnviarAsync(HttpMethod.Post, "/v1/api-keys", cookie, new { nombre = "Ecommerce" });

        await AssertErrorAsync(response, HttpStatusCode.Forbidden, "SIN_PERMISOS");
        Assert.Equal(0, await ApiKeys().CountDocumentsAsync(FilterDefinition<ApiKey>.Empty));
    }

    [MongoFact]
    public async Task CP08_KeyDeOtraEmpresa_Devuelve404YLaKeyAjenaSigueActiva()
    {
        var cookieB = await LoginEmpresaAsync(EmailAdminB);
        var keyB = await CrearAsync(cookieB, "Ajena");
        var cookieA = await LoginEmpresaAsync(EmailAdminA);

        var revocar = await EnviarAsync(HttpMethod.Delete, $"/v1/api-keys/{keyB.Id}", cookieA);
        await AssertErrorAsync(revocar, HttpStatusCode.NotFound, "API_KEY_INVALIDA");

        var regenerar = await EnviarAsync(HttpMethod.Post, $"/v1/api-keys/{keyB.Id}/regenerar", cookieA);
        await AssertErrorAsync(regenerar, HttpStatusCode.NotFound, "API_KEY_INVALIDA");

        var documento = await ApiKeys().Find(a => a.Id == keyB.Id).SingleAsync();
        Assert.True(documento.Activa);
        Assert.Null(documento.RevocadaAt);
        Assert.Equal(_contribuyenteB!.Id, documento.ContribuyenteId);
    }

    [MongoFact]
    public async Task CP09_NombreVacioOBodyVacio_Devuelve400ValidacionYNoCreaNada()
    {
        var cookie = await LoginEmpresaAsync(EmailAdminA);

        var vacio = await EnviarAsync(HttpMethod.Post, "/v1/api-keys", cookie, new { nombre = "" });
        await AssertErrorAsync(vacio, HttpStatusCode.BadRequest, "VALIDACION");

        var sinNombre = await EnviarCrudoAsync("/v1/api-keys", cookie, "{}");
        await AssertErrorAsync(sinNombre, HttpStatusCode.BadRequest, "VALIDACION");

        var sinCuerpo = await EnviarCrudoAsync("/v1/api-keys", cookie, string.Empty);
        await AssertErrorAsync(sinCuerpo, HttpStatusCode.BadRequest, "VALIDACION");

        Assert.Equal(0, await ApiKeys().CountDocumentsAsync(FilterDefinition<ApiKey>.Empty));
    }

    [MongoFact]
    public async Task CP10_ContribuyenteInactivo_Devuelve403CuentaInactivaYNoCreaLaKey()
    {
        var cookie = await LoginEmpresaAsync(EmailAdminA);
        await Contribuyentes().UpdateOneAsync(
            c => c.Id == _contribuyenteA!.Id,
            Builders<Contribuyente>.Update.Set(c => c.Activo, false));

        var response = await EnviarAsync(HttpMethod.Post, "/v1/api-keys", cookie, new { nombre = "Ecommerce" });

        await AssertErrorAsync(response, HttpStatusCode.Forbidden, "CUENTA_INACTIVA");
        Assert.Equal(0, await ApiKeys().CountDocumentsAsync(FilterDefinition<ApiKey>.Empty));
    }

    [MongoFact]
    public async Task CP11_CrearVariasKeys_GeneraPrefijosDistintos()
    {
        var cookie = await LoginEmpresaAsync(EmailAdminA);

        var creadas = new List<ApiKeyCreadaResponse>();
        for (var i = 0; i < 5; i++)
        {
            creadas.Add(await CrearAsync(cookie, $"Key {i}"));
        }

        Assert.Equal(5, creadas.Select(c => c.Prefix).Distinct().Count());
        Assert.Equal(5, creadas.Select(c => c.Valor).Distinct().Count());
        Assert.All(creadas, c => Assert.Matches(FormatoValor, c.Valor));

        var documentos = await ApiKeys().Find(FilterDefinition<ApiKey>.Empty).ToListAsync();
        Assert.Equal(5, documentos.Select(d => d.Prefix).Distinct().Count());
        Assert.All(documentos, d => Assert.Matches(FormatoPrefijo, d.Prefix));
    }

    [MongoFact]
    public async Task CP13_CrearRevocarYRegenerar_RegistranEventosSinSecretos()
    {
        var cookie = await LoginEmpresaAsync(EmailAdminA);
        var creada = await CrearAsync(cookie, "Ecommerce");
        var hashCreada = (await ApiKeys().Find(a => a.Id == creada.Id).SingleAsync()).Hash;

        Assert.Equal(HttpStatusCode.NoContent,
            (await EnviarAsync(HttpMethod.Delete, $"/v1/api-keys/{creada.Id}", cookie)).StatusCode);
        var regenerada = await LeerAsync<ApiKeyRegeneradaResponse>(
            await EnviarAsync(HttpMethod.Post, $"/v1/api-keys/{creada.Id}/regenerar", cookie));
        var hashRegenerada = (await ApiKeys().Find(a => a.Id == creada.Id).SingleAsync()).Hash;

        var eventos = await Coleccion<EventoAuditoria>(MongoCollections.Eventos)
            .Find(e => e.Entidad == nameof(ApiKey) && e.EntidadId == creada.Id.ToString())
            .ToListAsync();

        Assert.Equal(3, eventos.Count);
        Assert.Contains(eventos, e => e.Accion == "apikey.creada");
        Assert.Contains(eventos, e => e.Accion == "apikey.revocada");
        Assert.Contains(eventos, e => e.Accion == "apikey.regenerada");
        Assert.All(eventos, e => Assert.Equal(_contribuyenteA!.Id, e.ContribuyenteId));

        var datos = string.Join('\n', eventos.Select(e => e.Datos ?? string.Empty));
        Assert.DoesNotContain(creada.Valor, datos, StringComparison.Ordinal);
        Assert.DoesNotContain(regenerada.Valor, datos, StringComparison.Ordinal);
        Assert.DoesNotContain(creada.Valor.Split('_')[2], datos, StringComparison.Ordinal);
        Assert.DoesNotContain(regenerada.Valor.Split('_')[2], datos, StringComparison.Ordinal);
        Assert.DoesNotContain(hashCreada, datos, StringComparison.Ordinal);
        Assert.DoesNotContain(hashRegenerada, datos, StringComparison.Ordinal);
    }

    [MongoFact]
    public async Task CP14_TresKeysActivas_ConvivenYElListadoLasDevuelve()
    {
        var cookie = await LoginEmpresaAsync(EmailAdminA);
        var nombres = new[] { "Ecommerce", "Facturación", "Integraciones" };
        foreach (var nombre in nombres)
        {
            await CrearAsync(cookie, nombre);
        }

        var listado = await LeerAsync<ApiKeyListResponse>(
            await EnviarAsync(HttpMethod.Get, "/v1/api-keys", cookie));

        Assert.Equal(3, listado.Items.Count);
        Assert.All(listado.Items, i => Assert.True(i.Activa));
        Assert.Equal(3, listado.Items.Select(i => i.Prefix).Distinct().Count());
        Assert.Equal(
            nombres.OrderBy(n => n, StringComparer.Ordinal).ToList(),
            listado.Items.Select(i => i.Nombre).OrderBy(n => n, StringComparer.Ordinal).ToList());
    }

    [MongoFact]
    public async Task CP15_SuperAdmin_Devuelve403SinPermisos()
    {
        var cookie = await LoginAdminAsync(SuperAdminEmail);

        var response = await EnviarAsync(HttpMethod.Get, "/v1/api-keys", cookie);

        await AssertErrorAsync(response, HttpStatusCode.Forbidden, "SIN_PERMISOS");
    }

    private async Task<ApiKeyCreadaResponse> CrearAsync(string cookie, string nombre)
    {
        var response = await EnviarAsync(HttpMethod.Post, "/v1/api-keys", cookie, new { nombre });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var creada = await LeerAsync<ApiKeyCreadaResponse>(response);
        Assert.Matches(FormatoValor, creada.Valor);
        return creada;
    }

    private async Task<string> LoginEmpresaAsync(string email)
        => await LoginAsync("/v1/auth/login", email, EmpresaPassword);

    private async Task<string> LoginAdminAsync(string email)
        => await LoginAsync("/v1/admin/auth/login", email, SuperAdminPassword);

    private async Task<string> LoginAsync(string ruta, string email, string password)
    {
        var response = await Client.PostAsJsonAsync(ruta, new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return response.Headers.GetValues("Set-Cookie").First().Split(';')[0];
    }

    private async Task<HttpResponseMessage> EnviarAsync(
        HttpMethod metodo,
        string ruta,
        string cookie,
        object? body = null)
    {
        using var request = new HttpRequestMessage(metodo, ruta);
        request.Headers.TryAddWithoutValidation("Cookie", cookie);

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await Client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> EnviarCrudoAsync(string ruta, string cookie, string json)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, ruta)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.TryAddWithoutValidation("Cookie", cookie);
        return await Client.SendAsync(request);
    }

    private static async Task<T> LeerAsync<T>(HttpResponseMessage response)
        => (await response.Content.ReadFromJsonAsync<T>())
            ?? throw new InvalidOperationException($"La respuesta no contiene {typeof(T).Name}.");

    private static async Task<ErrorResponse> AssertErrorAsync(
        HttpResponseMessage response,
        HttpStatusCode status,
        string codigo)
    {
        Assert.Equal(status, response.StatusCode);
        var error = await LeerAsync<ErrorResponse>(response);
        Assert.Equal(codigo, error.Codigo);
        return error;
    }

    private Usuario NuevoUsuario(Guid contribuyenteId, string email, RolUsuario rol)
        => new()
        {
            ContribuyenteId = contribuyenteId,
            Email = email,
            PasswordHash = _hasher.Hash(EmpresaPassword),
            Rol = rol,
            Activo = true
        };

    private HttpClient Client => _client
        ?? throw new InvalidOperationException("El test de MongoDB no fue inicializado.");

    private IMongoCollection<ApiKey> ApiKeys() => Coleccion<ApiKey>(MongoCollections.ApiKeys);

    private IMongoCollection<Contribuyente> Contribuyentes()
        => Coleccion<Contribuyente>(MongoCollections.Contribuyentes);

    private IMongoCollection<Usuario> Usuarios() => Coleccion<Usuario>(MongoCollections.Usuarios);

    private IMongoCollection<T> Coleccion<T>(string nombre)
        => _mongoClient!.GetDatabase(_databaseName).GetCollection<T>(nombre);
}
