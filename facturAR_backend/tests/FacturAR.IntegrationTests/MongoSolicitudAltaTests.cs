using System.Net;
using System.Net.Http.Json;
using FacturAR.Application.Notificaciones;
using FacturAR.Contracts.Solicitudes;
using FacturAR.Domain.Entities;
using FacturAR.Domain.Enums;
using FacturAR.Infrastructure.Mongo;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MongoDB.Bson;
using MongoDB.Driver;

namespace FacturAR.IntegrationTests;

public sealed class MongoSolicitudAltaTests : IAsyncLifetime
{
    private const string CuitValido = "20123456786";
    private const string OtroCuitValido = "30712345671";
    private const string EmailValido = "solicitante@demo.com";

    private readonly string _databaseName = $"facturar_it_{Guid.NewGuid():N}"[..28];
    private readonly FakeEmailSender _emails = new();

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
                builder.UseSetting("Admin:NotificationEmail", string.Empty);
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<IEmailSender>();
                    services.AddSingleton<IEmailSender>(_emails);
                });
            });

        _client = _factory.CreateClient();
        _mongoClient = _factory.Services.GetRequiredService<IMongoClient>();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var mongoContext = _factory.Services.GetRequiredService<MongoDbContext>();
        await MongoIndexInitializer.InitializeAsync(mongoContext, timeout.Token);
    }

    [MongoFact]
    public async Task Post_ConSolicitudValida_PersisteSolicitudSinCrearOtrasEntidades()
    {
        var response = await PostAsync(Request());

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var solicitud = await GetSolicitudes().Find(FilterDefinition<SolicitudAltaEmpresa>.Empty).SingleAsync();
        Assert.Equal(CuitValido, solicitud.Cuit);
        Assert.Equal(EmailValido, solicitud.EmailSolicitante);
        Assert.Equal(EstadoSolicitud.Pendiente, solicitud.Estado);
        Assert.True(solicitud.AceptaTerminos);
        Assert.Equal(DateTimeKind.Utc, solicitud.CreatedAt.Kind);
        Assert.Equal(DateTimeKind.Utc, solicitud.UpdatedAt.Kind);
        Assert.Equal(DateTimeKind.Utc, solicitud.TerminosAceptadosAt.Kind);
        Assert.Null(solicitud.ContribuyenteId);
        Assert.Null(solicitud.InvitacionId);
        Assert.Null(solicitud.MotivoRechazo);

        Assert.Equal(0, await CountAsync(MongoCollections.Contribuyentes));
        Assert.Equal(0, await CountAsync(MongoCollections.Usuarios));
        Assert.Equal(0, await CountAsync(MongoCollections.PuntosVenta));
        Assert.Equal(0, await CountAsync(MongoCollections.ApiKeys));

        var mensaje = Assert.Single(_emails.Enviados);
        Assert.Equal(EmailValido, mensaje.Para);
        Assert.Equal(PlantillasEmail.NombreSolicitudRecibida, mensaje.Plantilla);
    }

    [MongoFact]
    public async Task Post_ConCuitPendiente_DevuelveConflictSinInsertar()
    {
        await GetSolicitudes().InsertOneAsync(Solicitud(EstadoSolicitud.Pendiente, CuitValido, "pendiente@demo.com"));

        var response = await PostAsync(Request() with { Email = "nuevo@demo.com" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("SOLICITUD_PENDIENTE", error.Codigo);
        Assert.Equal(1, await CountAsync(MongoCollections.SolicitudesAlta));
        Assert.Empty(_emails.Enviados);
    }

    [MongoFact]
    public async Task Post_ConEmailPendiente_DevuelveConflictSinInsertar()
    {
        await GetSolicitudes().InsertOneAsync(Solicitud(EstadoSolicitud.Pendiente, OtroCuitValido, "pendiente@demo.com"));

        var response = await PostAsync(Request() with { Email = "pendiente@demo.com" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("SOLICITUD_PENDIENTE", error.Codigo);
        Assert.Equal(1, await CountAsync(MongoCollections.SolicitudesAlta));
        Assert.Empty(_emails.Enviados);
    }

    [MongoFact]
    public async Task Post_ConCuitDeContribuyente_DevuelveAcceptedYAuditaSinInsertar()
    {
        await GetCollection<Contribuyente>(MongoCollections.Contribuyentes).InsertOneAsync(new Contribuyente
        {
            Cuit = CuitValido,
            RazonSocial = "Empresa Existente S.A."
        });

        var response = await PostAsync(Request());

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(0, await CountAsync(MongoCollections.SolicitudesAlta));
        Assert.Empty(_emails.Enviados);

        var evento = await GetCollection<EventoAuditoria>(MongoCollections.Eventos)
            .Find(e => e.Accion == "solicitud.cuit_registrado")
            .SingleAsync();
        Assert.Contains(CuitValido, evento.Datos);
    }

    [MongoFact]
    public async Task InicializarIndices_CreaLosTresIndicesConLosUnicosParciales()
    {
        var indices = await (await GetSolicitudes().Indexes.ListAsync()).ToListAsync();

        var cuit = Indice(indices, "ux_solicitudes_alta_cuit_pendiente");
        Assert.True(cuit["unique"].AsBoolean);
        Assert.True(cuit.Contains("partialFilterExpression"));
        Assert.True(cuit["partialFilterExpression"].AsBsonDocument.Contains("Estado"));

        var email = Indice(indices, "ux_solicitudes_alta_email_pendiente");
        Assert.True(email["unique"].AsBoolean);
        Assert.True(email.Contains("partialFilterExpression"));
        Assert.True(email["partialFilterExpression"].AsBsonDocument.Contains("Estado"));

        var estadoFecha = Indice(indices, "ix_solicitudes_alta_estado_fecha");
        Assert.False(estadoFecha.Contains("unique") && estadoFecha["unique"].AsBoolean);
        Assert.False(estadoFecha.Contains("partialFilterExpression"));
    }

    [MongoFact]
    public async Task Post_TrasUnaSolicitudRechazada_PermiteUnaNuevaPendiente()
    {
        await GetSolicitudes().InsertOneAsync(Solicitud(EstadoSolicitud.Rechazada, CuitValido, EmailValido));

        var response = await PostAsync(Request());

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(1, await GetSolicitudes()
            .CountDocumentsAsync(s => s.Cuit == CuitValido && s.Estado == EstadoSolicitud.Pendiente));
        Assert.Equal(2, await CountAsync(MongoCollections.SolicitudesAlta));
    }

    [MongoFact]
    public async Task Post_ConDosInsercionesConcurrentesDelMismoCuit_DejaUnaSolaPendiente()
    {
        var primera = PostAsync(Request() with { Email = "concurrente-1@demo.com" }, "203.0.113.10");
        var segunda = PostAsync(Request() with { Email = "concurrente-2@demo.com" }, "203.0.113.11");

        var respuestas = await Task.WhenAll(primera, segunda);

        Assert.Contains(respuestas, r => r.StatusCode == HttpStatusCode.Accepted);
        var conflictiva = Assert.Single(respuestas, r => r.StatusCode == HttpStatusCode.Conflict);
        var error = await conflictiva.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("SOLICITUD_PENDIENTE", error.Codigo);

        Assert.Equal(1, await GetSolicitudes()
            .CountDocumentsAsync(s => s.Cuit == CuitValido && s.Estado == EstadoSolicitud.Pendiente));
    }

    [MongoFact]
    public async Task Post_ConEmailFallido_DejaLaSolicitudPersistidaYAuditaElFallo()
    {
        _emails.Excepcion = new InvalidOperationException("SMTP caído.");

        var response = await PostAsync(Request());

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(1, await CountAsync(MongoCollections.SolicitudesAlta));

        var evento = await GetCollection<EventoAuditoria>(MongoCollections.Eventos)
            .Find(e => e.Accion == "email.fallido")
            .SingleAsync();
        Assert.Contains(CuitValido, evento.Datos);
    }

    public async Task DisposeAsync()
    {
        if (_mongoClient is not null)
        {
            await _mongoClient.DropDatabaseAsync(_databaseName);
        }

        _client?.Dispose();
        _factory?.Dispose();
    }

    private HttpClient Client => _client
        ?? throw new InvalidOperationException("El test de MongoDB no fue inicializado.");

    private IMongoClient Mongo => _mongoClient
        ?? throw new InvalidOperationException("El test de MongoDB no fue inicializado.");

    private IMongoCollection<SolicitudAltaEmpresa> GetSolicitudes()
        => GetCollection<SolicitudAltaEmpresa>(MongoCollections.SolicitudesAlta);

    private IMongoCollection<T> GetCollection<T>(string nombre)
        => Mongo.GetDatabase(_databaseName).GetCollection<T>(nombre);

    private async Task<long> CountAsync(string coleccion)
        => await Mongo.GetDatabase(_databaseName)
            .GetCollection<BsonDocument>(coleccion)
            .CountDocumentsAsync(FilterDefinition<BsonDocument>.Empty);

    private async Task<HttpResponseMessage> PostAsync(SolicitudAltaRequest request, string ip = "203.0.113.20")
    {
        using var mensaje = new HttpRequestMessage(HttpMethod.Post, "/v1/solicitudes-alta")
        {
            Content = JsonContent.Create(request)
        };
        mensaje.Headers.Add("X-Forwarded-For", ip);

        return await Client.SendAsync(mensaje);
    }

    private static BsonDocument Indice(IEnumerable<BsonDocument> indices, string nombre)
        => indices.Single(i => i["name"].AsString == nombre);

    private static SolicitudAltaEmpresa Solicitud(
        EstadoSolicitud estado,
        string cuit,
        string email) => new()
        {
            RazonSocial = "Solicitud Existente S.A.",
            Cuit = cuit,
            NombreSolicitante = "Ana Pérez",
            EmailSolicitante = email,
            Telefono = "+54 11 5555-4444",
            Domicilio = "Av. Corrientes 1234",
            Ciudad = "Buenos Aires",
            Provincia = "CABA",
            CodigoPostal = "C1043",
            AceptaTerminos = true,
            Estado = estado,
            CreatedAt = new DateTime(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc),
            TerminosAceptadosAt = new DateTime(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc)
        };

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
