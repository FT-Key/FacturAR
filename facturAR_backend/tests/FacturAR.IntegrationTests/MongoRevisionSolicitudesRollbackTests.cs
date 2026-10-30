using System.Net;
using System.Net.Http.Json;
using FacturAR.Application.Abstractions;
using FacturAR.Application.Notificaciones;
using FacturAR.Contracts.Auth;
using FacturAR.Domain.Entities;
using FacturAR.Domain.Enums;
using FacturAR.Infrastructure.Mongo;
using FacturAR.Infrastructure.Password;
using FacturAR.Infrastructure.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MongoDB.Driver;

namespace FacturAR.IntegrationTests;

public sealed class MongoRevisionSolicitudesRollbackTests : IAsyncLifetime
{
    private const string CuitValido = "20123456786";
    private const string EmailSolicitante = "solicitante@demo.com";
    private const string SuperAdminEmail = "super@facturar.ar";
    private const string Password = "SuperSecreta123!";
    private const string Base = "/v1/admin/solicitudes";

    private readonly string _databaseName = $"facturar_it_{Guid.NewGuid():N}"[..28];
    private readonly FakeEmailSender _emails = new();
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

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.UseSetting("Mongo:ConnectionString", MongoTestSettings.GetConnectionStringWithTimeout());
                builder.UseSetting("Mongo:DatabaseName", _databaseName);
                builder.UseSetting("Mongo:InitializeIndexes", "true");
                builder.UseSetting("Auth:Login:RateLimitPorIp", "1000");
                builder.UseSetting("BootstrapAdmin:Email", string.Empty);
                builder.UseSetting("Admin:NotificationEmail", string.Empty);
                builder.UseSetting("Portal:BaseUrl", "https://portal.test");
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IEmailSender>();
                    services.AddSingleton<IEmailSender>(_emails);

                    services.RemoveAll<IEventoAuditoriaRepository>();
                    services.AddScoped<IEventoAuditoriaRepository>(sp => new AuditoriaQueFalla(
                        new EventoAuditoriaRepository(
                            sp.GetRequiredService<MongoDbContext>(),
                            sp.GetRequiredService<MongoSesionActual>())));
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

        await SembrarAdministradorAsync(SuperAdminEmail);
    }

    public Task DisposeAsync()
    {
        _mongoClient?.DropDatabase(_databaseName);
        _client?.Dispose();
        _factory?.Dispose();
        return Task.CompletedTask;
    }

    [MongoFact]
    public async Task Aprobar_ConFallaDentroDeLaTransaccion_NoPersisteNada()
    {
        var solicitud = await SembrarSolicitudAsync();
        var cookie = await LoginAsync(SuperAdminEmail);

        var response = await PostAsync($"{Base}/{solicitud.Id}/aprobar", cookie);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        var guardada = await ObtenerSolicitudAsync(solicitud.Id);
        Assert.Equal(EstadoSolicitud.Pendiente, guardada.Estado);
        Assert.Null(guardada.ContribuyenteId);
        Assert.Null(guardada.InvitacionId);
        Assert.Null(guardada.RevisadaPorAdministradorId);
        Assert.Null(guardada.RevisadaAt);

        Assert.Equal(0, await ContarAsync<Contribuyente>(MongoCollections.Contribuyentes));
        Assert.Equal(0, await ContarAsync<PuntoVenta>(MongoCollections.PuntosVenta));
        Assert.Equal(0, await ContarAsync<Invitacion>(MongoCollections.Invitaciones));
        Assert.Empty(await EventosDeAsync(solicitud.Id));
        Assert.Empty(_emails.Enviados);
    }

    private async Task<Guid> SembrarAdministradorAsync(string email)
    {
        var administrador = new AdministradorFacturar
        {
            Nombre = "SuperAdmin",
            Email = email,
            PasswordHash = _hasher.Hash(Password),
            Rol = RolGlobal.SuperAdmin,
            Activo = true
        };
        await Coleccion<AdministradorFacturar>(MongoCollections.Administradores).InsertOneAsync(administrador);
        return administrador.Id;
    }

    private async Task<SolicitudAltaEmpresa> SembrarSolicitudAsync()
    {
        var ahora = DateTime.UtcNow.AddMinutes(-5);
        var solicitud = new SolicitudAltaEmpresa
        {
            RazonSocial = "ACME S.A.",
            Cuit = CuitValido,
            CondicionIva = CondicionIva.ResponsableInscripto,
            NombreSolicitante = "Ana Pérez",
            EmailSolicitante = EmailSolicitante,
            Telefono = "1145678901",
            Domicilio = "Av. Corrientes 1234",
            Ciudad = "CABA",
            Provincia = "CABA",
            CodigoPostal = "1043",
            Estado = EstadoSolicitud.Pendiente,
            CreatedAt = ahora,
            UpdatedAt = ahora
        };
        await Coleccion<SolicitudAltaEmpresa>(MongoCollections.SolicitudesAlta).InsertOneAsync(solicitud);
        return solicitud;
    }

    private async Task<string> LoginAsync(string email)
    {
        var response = await _client!.PostAsJsonAsync("/v1/admin/auth/login", new LoginRequest(email, Password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return response.Headers.GetValues("Set-Cookie").First().Split(';')[0];
    }

    private Task<HttpResponseMessage> PostAsync(string ruta, string cookie)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, ruta);
        request.Headers.TryAddWithoutValidation("Cookie", cookie);
        return _client!.SendAsync(request);
    }

    private Task<SolicitudAltaEmpresa> ObtenerSolicitudAsync(Guid id)
        => Coleccion<SolicitudAltaEmpresa>(MongoCollections.SolicitudesAlta).Find(s => s.Id == id).SingleAsync();

    private Task<long> ContarAsync<T>(string coleccion)
        => Coleccion<T>(coleccion).CountDocumentsAsync(FilterDefinition<T>.Empty);

    private Task<List<EventoAuditoria>> EventosDeAsync(Guid solicitudId)
        => Coleccion<EventoAuditoria>(MongoCollections.Eventos)
            .Find(e => e.EntidadId == solicitudId.ToString())
            .ToListAsync();

    private IMongoCollection<T> Coleccion<T>(string nombre)
        => _mongoClient!.GetDatabase(_databaseName).GetCollection<T>(nombre);

    private sealed class AuditoriaQueFalla(IEventoAuditoriaRepository interno) : IEventoAuditoriaRepository
    {
        public Task AddAsync(EventoAuditoria evento, CancellationToken ct = default)
        {
            if (evento.Accion == "solicitud.aprobada")
            {
                throw new InvalidOperationException("Falla forzada de auditoría.");
            }

            return interno.AddAsync(evento, ct);
        }

        public Task<IReadOnlyList<EventoAuditoria>> ListarPorEntidadAsync(
            string entidad,
            string entidadId,
            CancellationToken ct = default)
            => interno.ListarPorEntidadAsync(entidad, entidadId, ct);
    }
}
