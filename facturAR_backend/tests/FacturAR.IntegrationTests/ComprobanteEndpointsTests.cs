using System.Net;
using System.Net.Http.Json;
using System.Text;
using FacturAR.Application.Abstractions;
using FacturAR.Application.ApiKeys;
using FacturAR.Application.Comprobantes;
using FacturAR.Contracts.Auth;
using FacturAR.Contracts.Comunes;
using FacturAR.Contracts.Comprobantes;
using FacturAR.Domain.Entities;
using FacturAR.Domain.Enums;
using FacturAR.Infrastructure.Password;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FacturAR.IntegrationTests;

public sealed class ComprobanteEndpointsTests
{
    [Fact]
    public async Task Emitir_SinHeaderDeApiKey_Devuelve401ApiKeyAusente()
    {
        using var factory = new ComprobanteApiKeyFactory();
        using var client = factory.CreateClient();

        var response = await EnviarAsync(client, FacturaA(), "idem-sin-key", apiKey: null);

        await AssertErrorAsync(response, HttpStatusCode.Unauthorized, "API_KEY_AUSENTE");
    }

    [Fact]
    public async Task Emitir_ConApiKeyInexistente_Devuelve401ApiKeyInvalida()
    {
        using var factory = new ComprobanteApiKeyFactory();
        using var client = factory.CreateClient();

        var response = await EnviarAsync(
            client,
            FacturaA(),
            "idem-key-invalida",
            ComprobanteApiKeyFactory.ApiKeyDesconocida);

        await AssertErrorAsync(response, HttpStatusCode.Unauthorized, "API_KEY_INVALIDA");
    }

    [Fact]
    public async Task Emitir_ConContribuyenteInactivo_Devuelve403CuentaInactiva()
    {
        using var factory = new ComprobanteApiKeyFactory();
        var contribuyente = factory.Contribuyentes.Contribuyentes
            .Single(c => c.Id == ComprobanteApiKeyFactory.ContribuyenteId);
        contribuyente.Activo = false;
        using var client = factory.CreateClient();

        var response = await EnviarAsync(
            client,
            FacturaA(),
            "idem-cuenta-inactiva",
            ComprobanteApiKeyFactory.ApiKeyValida);

        var error = await AssertErrorAsync(response, HttpStatusCode.Forbidden, "CUENTA_INACTIVA");
        Assert.Equal("El contribuyente está inactivo.", error.Mensaje);
        Assert.Empty(factory.Comprobantes.Comprobantes);
        Assert.Equal(0, factory.Secuencias.Llamadas);
        Assert.Empty(factory.Eventos.Eventos);
    }

    [Fact]
    public async Task Emitir_SinIdempotencyKey_Devuelve400Validacion()
    {
        using var factory = new ComprobanteApiKeyFactory();
        using var client = factory.CreateClient();

        var response = await EnviarAsync(
            client,
            FacturaA(),
            idempotencyKey: null,
            ComprobanteApiKeyFactory.ApiKeyValida);

        var error = await AssertErrorAsync(response, HttpStatusCode.BadRequest, "VALIDACION");
        Assert.Equal("Falta el header Idempotency-Key.", error.Mensaje);
        Assert.Empty(factory.Eventos.Eventos);
    }

    [Fact]
    public async Task Emitir_ConIdempotencyKeyDe129Caracteres_Devuelve400Validacion()
    {
        using var factory = new ComprobanteApiKeyFactory();
        using var client = factory.CreateClient();

        var response = await EnviarAsync(
            client,
            FacturaA(),
            new string('a', 129),
            ComprobanteApiKeyFactory.ApiKeyValida);

        var error = await AssertErrorAsync(response, HttpStatusCode.BadRequest, "VALIDACION");
        Assert.Equal("El header Idempotency-Key admite hasta 128 caracteres.", error.Mensaje);
        Assert.Empty(factory.Eventos.Eventos);
    }

    [Fact]
    public async Task Emitir_ConCookieDePortalSinApiKey_Devuelve401ApiKeyAusente()
    {
        using var factory = new ComprobanteApiKeyFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false,
            AllowAutoRedirect = false
        });

        var login = await client.PostAsJsonAsync(
            "/v1/auth/login",
            new LoginRequest(ComprobanteApiKeyFactory.EmailUsuario, ComprobanteApiKeyFactory.PasswordUsuario));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var cookie = login.Headers.GetValues("Set-Cookie").First().Split(';')[0];

        using var mensaje = new HttpRequestMessage(HttpMethod.Post, "/v1/comprobantes")
        {
            Content = JsonContent.Create(FacturaA())
        };
        mensaje.Headers.TryAddWithoutValidation("Idempotency-Key", "idem-cookie");
        mensaje.Headers.TryAddWithoutValidation("Cookie", cookie);

        var response = await client.SendAsync(mensaje);

        await AssertErrorAsync(response, HttpStatusCode.Unauthorized, "API_KEY_AUSENTE");
    }

    [Fact]
    public async Task Emitir_ConItemsConElementoNulo_Devuelve400Validacion()
    {
        using var factory = new ComprobanteApiKeyFactory();
        using var client = factory.CreateClient();

        const string cuerpo = """
        {
          "tipoComprobante": 1,
          "puntoVenta": 1,
          "receptor": {
            "documentoTipo": "CUIT",
            "documentoNumero": "30712345671",
            "razonSocial": "Cliente S.A.",
            "condicionIva": 1
          },
          "items": [null]
        }
        """;

        using var mensaje = new HttpRequestMessage(HttpMethod.Post, "/v1/comprobantes")
        {
            Content = new StringContent(cuerpo, Encoding.UTF8, "application/json")
        };
        mensaje.Headers.TryAddWithoutValidation("X-Api-Key", ComprobanteApiKeyFactory.ApiKeyValida);
        mensaje.Headers.TryAddWithoutValidation("Idempotency-Key", "idem-items-nulos");

        var response = await client.SendAsync(mensaje);

        var error = await AssertErrorAsync(response, HttpStatusCode.BadRequest, "VALIDACION");
        Assert.Equal("Los ítems del comprobante no son válidos.", error.Mensaje);
        Assert.Empty(factory.Eventos.Eventos);
    }

    [Theory]
    [InlineData("{ \"tipoComprobante\": ")]
    [InlineData("null")]
    public async Task Emitir_ConBodyMalformado_Devuelve400Validacion(string cuerpo)
    {
        using var factory = new ComprobanteApiKeyFactory();
        using var client = factory.CreateClient();

        using var mensaje = new HttpRequestMessage(HttpMethod.Post, "/v1/comprobantes")
        {
            Content = new StringContent(cuerpo, Encoding.UTF8, "application/json")
        };
        mensaje.Headers.TryAddWithoutValidation("X-Api-Key", ComprobanteApiKeyFactory.ApiKeyValida);
        mensaje.Headers.TryAddWithoutValidation("Idempotency-Key", "idem-body-malformado");

        var response = await client.SendAsync(mensaje);

        var error = await AssertErrorAsync(response, HttpStatusCode.BadRequest, "VALIDACION");
        Assert.Equal("El cuerpo de la solicitud no es válido.", error.Mensaje);
        Assert.Empty(factory.Eventos.Eventos);
    }

    [Fact]
    public async Task CP0601_ReintentoConMismaClaveYBody_Devuelve200ConElMismoComprobante()
    {
        using var factory = new ComprobanteApiKeyFactory();
        using var client = factory.CreateClient();

        var primero = await EnviarAsync(
            client,
            FacturaA(),
            "pedido-001",
            ComprobanteApiKeyFactory.ApiKeyValida);
        Assert.Equal(HttpStatusCode.Created, primero.StatusCode);
        Assert.Equal("pedido-001", primero.Headers.GetValues("Idempotency-Key").Single());
        var original = await primero.Content.ReadFromJsonAsync<ComprobanteResponse>();
        Assert.NotNull(original);

        var segundo = await EnviarAsync(
            client,
            FacturaA(),
            "pedido-001",
            ComprobanteApiKeyFactory.ApiKeyValida);
        Assert.Equal(HttpStatusCode.OK, segundo.StatusCode);
        Assert.Equal("pedido-001", segundo.Headers.GetValues("Idempotency-Key").Single());
        var repetido = await segundo.Content.ReadFromJsonAsync<ComprobanteResponse>();
        Assert.NotNull(repetido);

        Assert.Equal(original!.Id, repetido!.Id);
        Assert.Equal(original.Numero, repetido.Numero);
        Assert.Equal(original.Cae, repetido.Cae);
        Assert.Single(factory.Comprobantes.Comprobantes);
        Assert.Equal(1, factory.Secuencias.Llamadas);
        Assert.Equal(1, factory.Transaccion.Ejecuciones);
        Assert.Single(factory.Eventos.Eventos);
    }

    [Fact]
    public async Task CP0602_MismaClaveConImporteDistinto_Devuelve409IdempotenciaConflicto()
    {
        using var factory = new ComprobanteApiKeyFactory();
        using var client = factory.CreateClient();

        var primero = await EnviarAsync(
            client,
            FacturaA(),
            "pedido-001",
            ComprobanteApiKeyFactory.ApiKeyValida);
        Assert.Equal(HttpStatusCode.Created, primero.StatusCode);

        var distinto = FacturaA() with
        {
            ImporteTotal = 24200m,
            Items = [new ItemComprobanteRequest("Servicio", 2m, 10000m, 21m)]
        };
        var response = await EnviarAsync(
            client,
            distinto,
            "pedido-001",
            ComprobanteApiKeyFactory.ApiKeyValida);

        var error = await AssertErrorAsync(
            response,
            HttpStatusCode.Conflict,
            "IDEMPOTENCIA_CONFLICTO");
        Assert.Contains("payload diferente", error.Mensaje, StringComparison.Ordinal);
        Assert.Single(factory.Comprobantes.Comprobantes);
        Assert.Equal(1, factory.Secuencias.Llamadas);
    }

    [Fact]
    public async Task CP0603_MismaClaveConCantidadDosYDosComaCero_Devuelve200Replay()
    {
        using var factory = new ComprobanteApiKeyFactory();
        using var client = factory.CreateClient();

        var primero = await EnviarAsync(
            client,
            FacturaA(),
            "pedido-001",
            ComprobanteApiKeyFactory.ApiKeyValida);
        Assert.Equal(HttpStatusCode.Created, primero.StatusCode);
        var original = await primero.Content.ReadFromJsonAsync<ComprobanteResponse>();
        Assert.NotNull(original);

        var equivalente = FacturaA() with
        {
            Items = [new ItemComprobanteRequest("Servicio", 2.00m, 5000.00m, 21.00m)]
        };
        var segundo = await EnviarAsync(
            client,
            equivalente,
            "pedido-001",
            ComprobanteApiKeyFactory.ApiKeyValida);

        Assert.Equal(HttpStatusCode.OK, segundo.StatusCode);
        var repetido = await segundo.Content.ReadFromJsonAsync<ComprobanteResponse>();
        Assert.Equal(original!.Id, repetido!.Id);
        Assert.Equal(1, factory.Secuencias.Llamadas);
    }

    [Fact]
    public async Task CP0604_MismaClaveConItemsEnDistintoOrden_Devuelve409()
    {
        using var factory = new ComprobanteApiKeyFactory();
        using var client = factory.CreateClient();

        var request = FacturaA() with
        {
            ImporteTotal = null,
            Items =
            [
                new ItemComprobanteRequest("Servicio A", 1m, 1000m, 21m),
                new ItemComprobanteRequest("Servicio B", 1m, 2000m, 21m)
            ]
        };
        var primero = await EnviarAsync(
            client,
            request,
            "pedido-001",
            ComprobanteApiKeyFactory.ApiKeyValida);
        Assert.Equal(HttpStatusCode.Created, primero.StatusCode);

        var invertido = request with
        {
            Items =
            [
                new ItemComprobanteRequest("Servicio B", 1m, 2000m, 21m),
                new ItemComprobanteRequest("Servicio A", 1m, 1000m, 21m)
            ]
        };
        var response = await EnviarAsync(
            client,
            invertido,
            "pedido-001",
            ComprobanteApiKeyFactory.ApiKeyValida);

        await AssertErrorAsync(response, HttpStatusCode.Conflict, "IDEMPOTENCIA_CONFLICTO");
        Assert.Equal(1, factory.Secuencias.Llamadas);
    }

    [Fact]
    public async Task CP0607_IdempotencyKeyDe128Caracteres_Devuelve201()
    {
        using var factory = new ComprobanteApiKeyFactory();
        using var client = factory.CreateClient();

        var response = await EnviarAsync(
            client,
            FacturaA(),
            new string('a', 128),
            ComprobanteApiKeyFactory.ApiKeyValida);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Single(factory.Comprobantes.Comprobantes);
    }

    [Fact]
    public async Task CP0610_MismaClaveEnOtroContribuyente_Devuelve201ConOtroComprobante()
    {
        using var factory = new ComprobanteApiKeyFactory();
        using var client = factory.CreateClient();

        var primero = await EnviarAsync(
            client,
            FacturaA(),
            "pedido-001",
            ComprobanteApiKeyFactory.ApiKeyValida);
        Assert.Equal(HttpStatusCode.Created, primero.StatusCode);
        var original = await primero.Content.ReadFromJsonAsync<ComprobanteResponse>();
        Assert.NotNull(original);

        var segundo = await EnviarAsync(
            client,
            FacturaA(),
            "pedido-001",
            ComprobanteApiKeyFactory.ApiKeyValidaB);
        Assert.Equal(HttpStatusCode.Created, segundo.StatusCode);
        var otro = await segundo.Content.ReadFromJsonAsync<ComprobanteResponse>();
        Assert.NotNull(otro);

        Assert.NotEqual(original!.Id, otro!.Id);
        Assert.Equal(2, factory.Comprobantes.Comprobantes.Count);
        Assert.Contains(
            factory.Comprobantes.Comprobantes,
            c => c.ContribuyenteId == ComprobanteApiKeyFactory.ContribuyenteIdB);
    }

    [Fact]
    public async Task CP0613_Emitir_SinApiKeyConIdempotencyKey_DevuelveEcoDelHeaderEnEl401()
    {
        using var factory = new ComprobanteApiKeyFactory();
        using var client = factory.CreateClient();

        var response = await EnviarAsync(client, FacturaA(), "pedido-001", apiKey: null);

        await AssertErrorAsync(response, HttpStatusCode.Unauthorized, "API_KEY_AUSENTE");
        Assert.Equal("pedido-001", response.Headers.GetValues("Idempotency-Key").Single());
    }

    private static EmitirComprobanteRequest FacturaA()
        => new(
            (int)TipoComprobante.FacturaA,
            1,
            null,
            new ReceptorComprobanteRequest(
                "CUIT",
                ComprobanteApiKeyFactory.CuitReceptor,
                "Cliente S.A.",
                (int)CondicionIva.ResponsableInscripto),
            [new ItemComprobanteRequest("Servicio", 2m, 5000m, 21m)],
            12100m,
            null);

    private static async Task<HttpResponseMessage> EnviarAsync(
        HttpClient client,
        EmitirComprobanteRequest request,
        string? idempotencyKey,
        string? apiKey)
    {
        using var mensaje = new HttpRequestMessage(HttpMethod.Post, "/v1/comprobantes")
        {
            Content = JsonContent.Create(request)
        };

        if (idempotencyKey is not null)
        {
            mensaje.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        }

        if (apiKey is not null)
        {
            mensaje.Headers.TryAddWithoutValidation("X-Api-Key", apiKey);
        }

        return await client.SendAsync(mensaje);
    }

    private static async Task<ErrorResponse> AssertErrorAsync(
        HttpResponseMessage response,
        HttpStatusCode status,
        string codigo)
    {
        Assert.Equal(status, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal(codigo, error.Codigo);
        return error;
    }
}

public sealed class ComprobanteApiKeyFactory : WebApplicationFactory<Program>
{
    public const string ApiKeyValida = "fak_0123456789ab_AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
    public const string ApiKeyValidaB = "fak_0123456789ac_EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE";
    public const string ApiKeyDesconocida = "fak_ffffffffffff_BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB";
    public const string EmailUsuario = "admin@demo.com";
    public const string PasswordUsuario = "Secreta123!";
    public const string CuitEmisor = "20123456786";
    public const string CuitEmisorB = "20111111112";
    public const string CuitReceptor = "30712345671";

    public static readonly Guid ContribuyenteId = Guid.NewGuid();
    public static readonly Guid ContribuyenteIdB = Guid.NewGuid();
    public static readonly Guid UsuarioId = Guid.NewGuid();
    public static readonly Guid ApiKeyId = Guid.NewGuid();
    public static readonly Guid ApiKeyIdB = Guid.NewGuid();

    private readonly PasswordHasher _hasher = new(1000);

    public ComprobanteApiKeyFactory()
    {
        Contribuyentes.Contribuyentes.Add(new Contribuyente
        {
            Id = ContribuyenteId,
            RazonSocial = "Demo SRL",
            Cuit = CuitEmisor,
            CondicionIva = CondicionIva.ResponsableInscripto,
            Activo = true
        });
        Contribuyentes.Contribuyentes.Add(new Contribuyente
        {
            Id = ContribuyenteIdB,
            RazonSocial = "Otra SRL",
            Cuit = CuitEmisorB,
            CondicionIva = CondicionIva.ResponsableInscripto,
            Activo = true
        });
        PuntosVenta.PuntosVenta.Add(new PuntoVenta
        {
            ContribuyenteId = ContribuyenteId,
            Numero = 1,
            Descripcion = "Principal",
            Activo = true
        });
        PuntosVenta.PuntosVenta.Add(new PuntoVenta
        {
            ContribuyenteId = ContribuyenteIdB,
            Numero = 1,
            Descripcion = "Principal",
            Activo = true
        });
        Usuarios.Usuario = new Usuario
        {
            Id = UsuarioId,
            ContribuyenteId = ContribuyenteId,
            Email = EmailUsuario,
            PasswordHash = _hasher.Hash(PasswordUsuario),
            Rol = RolUsuario.Administrador,
            Activo = true
        };
    }

    public ComprobanteContribuyenteFake Contribuyentes { get; } = new();

    public ComprobantePuntoVentaFake PuntosVenta { get; } = new();

    public ComprobanteParametrosFake Parametros { get; } = new();

    public ComprobanteUsuarioFake Usuarios { get; } = new();

    public ComprobanteEventoFake Eventos { get; } = new();

    public ComprobanteRepositorioEnMemoria Comprobantes { get; } = new();

    public ComprobanteSecuenciaEnMemoria Secuencias { get; } = new();

    public ComprobanteTransaccionEnMemoria Transaccion { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Mongo:InitializeIndexes", "false");
        builder.UseSetting("ApiKeys:ActualizarUltimoUso", "false");
        builder.UseSetting("Seed:Enabled", "false");
        builder.UseSetting("BootstrapAdmin:Email", string.Empty);
        builder.UseSetting("Auth:PasswordHashIterations", "1000");
        builder.UseSetting("Auth:Login:RateLimitPorIp", "1000");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IApiKeyAuthenticator>();
            services.AddSingleton<IApiKeyAuthenticator>(new ComprobanteApiKeyAuthenticatorFake(
                new Dictionary<string, ResultadoApiKey>(StringComparer.Ordinal)
                {
                    [ApiKeyValida] = new ResultadoApiKey(ApiKeyId, ContribuyenteId),
                    [ApiKeyValidaB] = new ResultadoApiKey(ApiKeyIdB, ContribuyenteIdB)
                }));

            services.RemoveAll<IContribuyenteRepository>();
            services.AddSingleton<IContribuyenteRepository>(Contribuyentes);

            services.RemoveAll<IPuntoVentaRepository>();
            services.AddSingleton<IPuntoVentaRepository>(PuntosVenta);

            services.RemoveAll<IParametrosResolver>();
            services.AddSingleton<IParametrosResolver>(Parametros);

            services.RemoveAll<IUsuarioRepository>();
            services.AddSingleton<IUsuarioRepository>(Usuarios);

            services.RemoveAll<IEventoAuditoriaRepository>();
            services.AddSingleton<IEventoAuditoriaRepository>(Eventos);

            services.RemoveAll<IComprobanteRepository>();
            services.AddSingleton<IComprobanteRepository>(Comprobantes);

            services.RemoveAll<ISecuenciaService>();
            services.AddSingleton<ISecuenciaService>(Secuencias);

            services.RemoveAll<ITransaccionMongo>();
            services.AddSingleton<ITransaccionMongo>(Transaccion);
        });
    }
}

public sealed class ComprobanteApiKeyAuthenticatorFake : IApiKeyAuthenticator
{
    private readonly IReadOnlyDictionary<string, ResultadoApiKey> _validas;

    public ComprobanteApiKeyAuthenticatorFake(IReadOnlyDictionary<string, ResultadoApiKey> validas)
    {
        _validas = validas;
    }

    public Task<ResultadoApiKey?> AutenticarAsync(string valorPresentado, CancellationToken ct = default)
        => Task.FromResult(
            _validas.TryGetValue(valorPresentado, out var resultado) ? resultado : null);
}

public sealed class ComprobanteContribuyenteFake : IContribuyenteRepository
{
    public List<Contribuyente> Contribuyentes { get; } = [];

    public Task<Contribuyente?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(Contribuyentes.FirstOrDefault(c => c.Id == id));

    public Task<bool> ExistePorCuitAsync(string cuit, CancellationToken ct = default)
        => Task.FromResult(Contribuyentes.Any(c => c.Cuit == cuit));

    public Task InsertarAsync(Contribuyente contribuyente, CancellationToken ct = default)
    {
        Contribuyentes.Add(contribuyente);
        return Task.CompletedTask;
    }
}

public sealed class ComprobantePuntoVentaFake : IPuntoVentaRepository
{
    public List<PuntoVenta> PuntosVenta { get; } = [];

    public Task InsertarAsync(PuntoVenta puntoVenta, CancellationToken ct = default)
    {
        PuntosVenta.Add(puntoVenta);
        return Task.CompletedTask;
    }

    public Task<PuntoVenta?> ObtenerPorNumeroAsync(
        Guid contribuyenteId,
        int numero,
        CancellationToken ct = default)
        => Task.FromResult(PuntosVenta.FirstOrDefault(
            p => p.ContribuyenteId == contribuyenteId && p.Numero == numero));
}

public sealed class ComprobanteParametrosFake : IParametrosResolver
{
    public Task<string?> ObtenerAsync(
        string clave,
        Guid? contribuyenteId,
        CancellationToken ct = default)
        => Task.FromResult<string?>(null);

    public Task<int> ObtenerDiasVencimientoCaeAsync(Guid? contribuyenteId, CancellationToken ct = default)
        => Task.FromResult(10);

    public Task<IReadOnlyList<decimal>> ObtenerAlicuotasPermitidasAsync(
        Guid? contribuyenteId,
        CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<decimal>>([0m, 5m, 10.5m, 21m, 27m]);

    public Task<string> ObtenerMonedaAsync(Guid? contribuyenteId, CancellationToken ct = default)
        => Task.FromResult("ARS");
}

public sealed class ComprobanteUsuarioFake : IUsuarioRepository
{
    public Usuario? Usuario { get; set; }

    public Task<Usuario?> GetByEmailAsync(string email, CancellationToken ct = default)
        => Task.FromResult(
            Usuario is not null && string.Equals(Usuario.Email, email, StringComparison.OrdinalIgnoreCase)
                ? Usuario
                : null);

    public Task<Usuario?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(Usuario is not null && Usuario.Id == id ? Usuario : null);

    public Task InsertAsync(Usuario usuario, CancellationToken ct = default)
    {
        Usuario = usuario;
        return Task.CompletedTask;
    }

    public Task<bool> ExistePorContribuyenteYEmailAsync(
        Guid contribuyenteId,
        string email,
        CancellationToken ct = default)
        => Task.FromResult(false);

    public Task UpdateAsync(Usuario usuario, CancellationToken ct = default)
    {
        Usuario = usuario;
        return Task.CompletedTask;
    }
}

public sealed class ComprobanteEventoFake : IEventoAuditoriaRepository
{
    public List<EventoAuditoria> Eventos { get; } = [];

    public Task AddAsync(EventoAuditoria evento, CancellationToken ct = default)
    {
        Eventos.Add(evento);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<EventoAuditoria>> ListarPorEntidadAsync(
        string entidad,
        string entidadId,
        CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<EventoAuditoria>>(
            Eventos.Where(e => e.Entidad == entidad && e.EntidadId == entidadId).ToList());
}

public sealed class ComprobanteRepositorioEnMemoria : IComprobanteRepository
{
    private readonly object _gate = new();
    private readonly List<Comprobante> _comprobantes = [];

    public IReadOnlyList<Comprobante> Comprobantes
    {
        get
        {
            lock (_gate)
            {
                return _comprobantes.ToList();
            }
        }
    }

    public Task<Comprobante?> GetByCaeAsync(string cae, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(_comprobantes.FirstOrDefault(c => c.Cae == cae));
        }
    }

    public Task<Comprobante?> GetByIdAsync(
        Guid contribuyenteId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(_comprobantes.FirstOrDefault(
                c => c.ContribuyenteId == contribuyenteId && c.Id == id));
        }
    }

    public Task<Comprobante?> GetByNumeracionAsync(
        Guid contribuyenteId,
        int puntoVenta,
        TipoComprobante tipo,
        long numero,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(_comprobantes.FirstOrDefault(
                c => c.ContribuyenteId == contribuyenteId
                    && c.PuntoVenta == puntoVenta
                    && c.TipoComprobante == tipo
                    && c.Numero == numero));
        }
    }

    public Task<Comprobante?> GetUltimoEmitidoAsync(
        Guid contribuyenteId,
        int puntoVenta,
        TipoComprobante tipo,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(_comprobantes
                .Where(c => c.ContribuyenteId == contribuyenteId
                    && c.PuntoVenta == puntoVenta
                    && c.TipoComprobante == tipo
                    && c.Estado == EstadoComprobante.Emitido)
                .OrderByDescending(c => c.Numero)
                .FirstOrDefault());
        }
    }

    public Task<(IReadOnlyList<Comprobante> Items, long Total)> ListarAsync(
        Guid contribuyenteId,
        FiltroListadoComprobantes filtro,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var coincidencias = _comprobantes
                .Where(c => c.ContribuyenteId == contribuyenteId)
                .OrderByDescending(c => c.FechaEmision)
                .ThenByDescending(c => c.Numero)
                .ThenBy(c => c.Id)
                .ToList();

            var items = coincidencias
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return Task.FromResult<(IReadOnlyList<Comprobante>, long)>((items, coincidencias.Count));
        }
    }

    public Task<Comprobante?> GetByIdempotencyKeyAsync(
        Guid contribuyenteId,
        string clave,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(_comprobantes.FirstOrDefault(
                c => c.ContribuyenteId == contribuyenteId && c.IdempotencyKey == clave));
        }
    }

    public Task AddAsync(Comprobante comprobante, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_comprobantes.Any(c =>
                c.ContribuyenteId == comprobante.ContribuyenteId
                && c.IdempotencyKey == comprobante.IdempotencyKey))
            {
                throw new IdempotenciaDuplicadaException();
            }

            _comprobantes.Add(comprobante);
        }

        return Task.CompletedTask;
    }

    public Task<bool> AjustarSaldoAsync(
        Guid contribuyenteId,
        Guid originalId,
        decimal? saldoEsperado,
        decimal saldoNuevo,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            var original = _comprobantes.FirstOrDefault(c =>
                c.Id == originalId
                && c.ContribuyenteId == contribuyenteId
                && c.ComprobanteOriginalId is null
                && c.ImporteOriginalAjustado == saldoEsperado);

            if (original is null)
            {
                return Task.FromResult(false);
            }

            original.ImporteOriginalAjustado = saldoNuevo;
            return Task.FromResult(true);
        }
    }
}

public sealed class ComprobanteSecuenciaEnMemoria : ISecuenciaService
{
    private readonly object _gate = new();
    private readonly Dictionary<(Guid, int, TipoComprobante), long> _numeros = [];

    public int Llamadas { get; private set; }

    public Task<long> ObtenerSiguienteNumeroAsync(
        Guid contribuyenteId,
        int puntoVenta,
        TipoComprobante tipo,
        CancellationToken ct = default)
    {
        lock (_gate)
        {
            var clave = (contribuyenteId, puntoVenta, tipo);
            var siguiente = _numeros.TryGetValue(clave, out var ultimo) ? ultimo + 1 : 1;
            _numeros[clave] = siguiente;
            Llamadas++;

            return Task.FromResult(siguiente);
        }
    }
}

public sealed class ComprobanteTransaccionEnMemoria : ITransaccionMongo
{
    public int Ejecuciones { get; private set; }

    public Task<T> EjecutarAsync<T>(
        Func<CancellationToken, Task<T>> operacion,
        CancellationToken ct = default)
    {
        Ejecuciones++;
        return operacion(ct);
    }
}