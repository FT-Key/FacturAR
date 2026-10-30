using System.Net;
using System.Net.Http.Json;
using FacturAR.Application.Notificaciones;
using FacturAR.Contracts.Auth;
using FacturAR.Contracts.Comunes;
using FacturAR.Contracts.Solicitudes;
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

public sealed class MongoRevisionSolicitudesTests : IAsyncLifetime
{
    private const string CuitValido = "20123456786";
    private const string OtroCuitValido = "30712345671";
    private const string EmailSolicitante = "solicitante@demo.com";
    private const string SuperAdminEmail = "super@facturar.ar";
    private const string SoporteEmail = "soporte@facturar.ar";
    private const string Password = "SuperSecreta123!";
    private const string Base = "/v1/admin/solicitudes";

    private readonly string _databaseName = $"facturar_it_{Guid.NewGuid():N}"[..28];
    private readonly FakeEmailSender _emails = new();
    private readonly PasswordHasher _hasher = new(1000);

    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;
    private IMongoClient? _mongoClient;
    private Guid _superAdminId;
    private Guid _soporteId;

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

        _superAdminId = await SembrarAdministradorAsync(SuperAdminEmail, RolGlobal.SuperAdmin);
        _soporteId = await SembrarAdministradorAsync(SoporteEmail, RolGlobal.Soporte);
    }

    public Task DisposeAsync()
    {
        _mongoClient?.DropDatabase(_databaseName);
        _client?.Dispose();
        _factory?.Dispose();
        return Task.CompletedTask;
    }

    [MongoFact]
    public async Task Aprobar_SolicitudValida_CreaTodoEnUnaTransaccionYEnviaLaInvitacion()
    {
        var solicitud = await SembrarSolicitudAsync();
        var cookie = await LoginAsync(SuperAdminEmail);

        var response = await PostAsync($"{Base}/{solicitud.Id}/aprobar", cookie);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var respuesta = await response.Content.ReadFromJsonAsync<AprobarSolicitudResponse>();
        Assert.NotNull(respuesta);
        Assert.True(respuesta.EmailEnviado);

        var contribuyente = await Coleccion<Contribuyente>(MongoCollections.Contribuyentes)
            .Find(FilterDefinition<Contribuyente>.Empty).SingleAsync();
        Assert.Equal(respuesta.ContribuyenteId, contribuyente.Id);
        Assert.Equal(CuitValido, contribuyente.Cuit);

        var puntoVenta = await Coleccion<PuntoVenta>(MongoCollections.PuntosVenta)
            .Find(FilterDefinition<PuntoVenta>.Empty).SingleAsync();
        Assert.Equal(contribuyente.Id, puntoVenta.ContribuyenteId);
        Assert.Equal(1, puntoVenta.Numero);
        Assert.Equal("Casa Central", puntoVenta.Descripcion);

        var invitacion = await Coleccion<Invitacion>(MongoCollections.Invitaciones)
            .Find(FilterDefinition<Invitacion>.Empty).SingleAsync();
        Assert.Equal(respuesta.InvitacionId, invitacion.Id);
        Assert.Equal(EmailSolicitante, invitacion.Email);
        Assert.Equal(RolUsuario.Administrador, invitacion.Rol);
        Assert.Equal(_superAdminId, invitacion.CreadaPorAdministradorId);
        Assert.InRange(invitacion.ExpiraAt, DateTime.UtcNow.AddHours(47), DateTime.UtcNow.AddHours(48));
        Assert.Equal(1, invitacion.IntentosEnvio);

        var guardada = await ObtenerSolicitudAsync(solicitud.Id);
        Assert.Equal(EstadoSolicitud.Aprobada, guardada.Estado);
        Assert.Equal(_superAdminId, guardada.RevisadaPorAdministradorId);
        Assert.Equal(contribuyente.Id, guardada.ContribuyenteId);
        Assert.Equal(invitacion.Id, guardada.InvitacionId);

        var email = Assert.Single(_emails.Enviados);
        Assert.Equal(PlantillasEmail.NombreSolicitudAprobada, email.Plantilla);
        Assert.Contains("https://portal.test/activar?token=", email.CuerpoTexto);

        var eventos = await EventosDeAsync(solicitud.Id);
        var aprobada = Assert.Single(eventos, e => e.Accion == "solicitud.aprobada");
        Assert.Equal(_superAdminId, aprobada.AdministradorId);
        Assert.Equal("Administrador", aprobada.ActorTipo);
        Assert.Null(aprobada.ContribuyenteId);
        Assert.Contains(eventos, e => e.Accion == "email.enviado");
    }

    [MongoFact]
    public async Task Aprobar_DosVeces_DevuelveConflictoSinDuplicar()
    {
        var solicitud = await SembrarSolicitudAsync();
        var cookie = await LoginAsync(SuperAdminEmail);
        Assert.Equal(HttpStatusCode.OK, (await PostAsync($"{Base}/{solicitud.Id}/aprobar", cookie)).StatusCode);

        var segunda = await PostAsync($"{Base}/{solicitud.Id}/aprobar", cookie);

        await AssertErrorAsync(segunda, HttpStatusCode.Conflict, "SOLICITUD_YA_PROCESADA");
        Assert.Equal(1, await ContarAsync<Contribuyente>(MongoCollections.Contribuyentes));
        Assert.Equal(1, await ContarAsync<PuntoVenta>(MongoCollections.PuntosVenta));
        Assert.Equal(1, await ContarAsync<Invitacion>(MongoCollections.Invitaciones));
        Assert.Single(_emails.Enviados);
    }

    [MongoFact]
    public async Task Aprobar_Concurrente_SoloUnaTransaccionConfirma()
    {
        var solicitud = await SembrarSolicitudAsync();
        var cookie = await LoginAsync(SuperAdminEmail);

        var respuestas = await Task.WhenAll(
            Enumerable.Range(0, 4).Select(_ => PostAsync($"{Base}/{solicitud.Id}/aprobar", cookie)));

        Assert.Single(respuestas, r => r.StatusCode == HttpStatusCode.OK);
        Assert.All(
            respuestas.Where(r => r.StatusCode != HttpStatusCode.OK),
            r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
        Assert.Equal(1, await ContarAsync<Contribuyente>(MongoCollections.Contribuyentes));
        Assert.Equal(1, await ContarAsync<PuntoVenta>(MongoCollections.PuntosVenta));
        Assert.Equal(1, await ContarAsync<Invitacion>(MongoCollections.Invitaciones));
    }

    [MongoFact]
    public async Task Aprobar_ConCuitYaRegistrado_DevuelveConflictoYDejaPendiente()
    {
        var solicitud = await SembrarSolicitudAsync();
        await Coleccion<Contribuyente>(MongoCollections.Contribuyentes)
            .InsertOneAsync(new Contribuyente { Cuit = CuitValido, RazonSocial = "Otra SA" });
        var cookie = await LoginAsync(SuperAdminEmail);

        var response = await PostAsync($"{Base}/{solicitud.Id}/aprobar", cookie);

        await AssertErrorAsync(response, HttpStatusCode.Conflict, "CUIT_YA_REGISTRADO");
        Assert.Equal(EstadoSolicitud.Pendiente, (await ObtenerSolicitudAsync(solicitud.Id)).Estado);
        Assert.Equal(1, await ContarAsync<Contribuyente>(MongoCollections.Contribuyentes));
        Assert.Equal(0, await ContarAsync<PuntoVenta>(MongoCollections.PuntosVenta));
        Assert.Equal(0, await ContarAsync<Invitacion>(MongoCollections.Invitaciones));
        Assert.Empty(_emails.Enviados);
    }

    [MongoFact]
    public async Task Aprobar_ConEmailCaido_ConfirmaLaAprobacionYDevuelveAdvertencia()
    {
        var solicitud = await SembrarSolicitudAsync();
        var cookie = await LoginAsync(SuperAdminEmail);
        _emails.Excepcion = new InvalidOperationException("SMTP caído");

        var response = await PostAsync($"{Base}/{solicitud.Id}/aprobar", cookie);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var respuesta = await response.Content.ReadFromJsonAsync<AprobarSolicitudResponse>();
        Assert.NotNull(respuesta);
        Assert.False(respuesta.EmailEnviado);
        Assert.False(string.IsNullOrWhiteSpace(respuesta.AdvertenciaEmail));
        Assert.Equal(1, await ContarAsync<Contribuyente>(MongoCollections.Contribuyentes));
        Assert.Equal(1, await ContarAsync<Invitacion>(MongoCollections.Invitaciones));
        Assert.Contains(await EventosDeAsync(solicitud.Id), e => e.Accion == "email.fallido");
    }

    [MongoFact]
    public async Task Aprobar_SolicitudInexistente_DevuelveNotFound()
    {
        var cookie = await LoginAsync(SuperAdminEmail);

        var response = await PostAsync($"{Base}/{Guid.NewGuid()}/aprobar", cookie);

        await AssertErrorAsync(response, HttpStatusCode.NotFound, "SOLICITUD_NO_ENCONTRADA");
    }

    [MongoFact]
    public async Task Rechazar_ConMotivo_DejaRechazadaSinCrearEmpresaYEnviaElMotivo()
    {
        var solicitud = await SembrarSolicitudAsync();
        var cookie = await LoginAsync(SuperAdminEmail);

        var response = await PostAsync(
            $"{Base}/{solicitud.Id}/rechazar",
            cookie,
            new RechazarSolicitudRequest("Datos fiscales incompletos"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var respuesta = await response.Content.ReadFromJsonAsync<RechazarSolicitudResponse>();
        Assert.Equal("Rechazada", respuesta!.Estado);

        var guardada = await ObtenerSolicitudAsync(solicitud.Id);
        Assert.Equal(EstadoSolicitud.Rechazada, guardada.Estado);
        Assert.Equal("Datos fiscales incompletos", guardada.MotivoRechazo);
        Assert.Equal(0, await ContarAsync<Contribuyente>(MongoCollections.Contribuyentes));
        Assert.Equal(0, await ContarAsync<Invitacion>(MongoCollections.Invitaciones));

        var email = Assert.Single(_emails.Enviados);
        Assert.Equal(PlantillasEmail.NombreSolicitudRechazada, email.Plantilla);
        Assert.Contains("Datos fiscales incompletos", email.CuerpoTexto);
        Assert.Contains(await EventosDeAsync(solicitud.Id), e => e.Accion == "solicitud.rechazada" && e.AdministradorId == _superAdminId);
    }

    [MongoFact]
    public async Task Rechazar_SinMotivo_DevuelveValidacionYDejaPendiente()
    {
        var solicitud = await SembrarSolicitudAsync();
        var cookie = await LoginAsync(SuperAdminEmail);

        var response = await PostAsync($"{Base}/{solicitud.Id}/rechazar", cookie, new RechazarSolicitudRequest("  "));

        await AssertErrorAsync(response, HttpStatusCode.BadRequest, "VALIDACION");
        Assert.Equal(EstadoSolicitud.Pendiente, (await ObtenerSolicitudAsync(solicitud.Id)).Estado);
        Assert.Empty(_emails.Enviados);
    }

    [MongoFact]
    public async Task Soporte_NoPuedeEditarAprobarNiRechazar()
    {
        var solicitud = await SembrarSolicitudAsync();
        var cookie = await LoginAsync(SoporteEmail);

        await AssertErrorAsync(
            await PostAsync($"{Base}/{solicitud.Id}/aprobar", cookie),
            HttpStatusCode.Forbidden,
            "SIN_PERMISOS");
        await AssertErrorAsync(
            await PostAsync($"{Base}/{solicitud.Id}/rechazar", cookie, new RechazarSolicitudRequest("Datos fiscales incompletos")),
            HttpStatusCode.Forbidden,
            "SIN_PERMISOS");
        await AssertErrorAsync(
            await SendAsync(HttpMethod.Put, $"{Base}/{solicitud.Id}", cookie, Edicion("Otra Razón SA")),
            HttpStatusCode.Forbidden,
            "SIN_PERMISOS");

        var guardada = await ObtenerSolicitudAsync(solicitud.Id);
        Assert.Equal(EstadoSolicitud.Pendiente, guardada.Estado);
        Assert.Equal("ACME S.A.", guardada.RazonSocial);
        Assert.Equal(0, await ContarAsync<Contribuyente>(MongoCollections.Contribuyentes));
    }

    [MongoFact]
    public async Task Soporte_PuedeListarYReenviarLaInvitacion()
    {
        var solicitud = await SembrarSolicitudAsync();
        Assert.Equal(
            HttpStatusCode.OK,
            (await PostAsync($"{Base}/{solicitud.Id}/aprobar", await LoginAsync(SuperAdminEmail))).StatusCode);
        var anterior = await Coleccion<Invitacion>(MongoCollections.Invitaciones)
            .Find(FilterDefinition<Invitacion>.Empty).SingleAsync();
        await Coleccion<Invitacion>(MongoCollections.Invitaciones).UpdateOneAsync(
            i => i.Id == anterior.Id,
            Builders<Invitacion>.Update.Set(i => i.ExpiraAt, DateTime.UtcNow.AddHours(-1)));
        _emails.Enviados.Clear();
        var cookie = await LoginAsync(SoporteEmail);

        var listado = await SendAsync(HttpMethod.Get, Base, cookie);
        Assert.Equal(HttpStatusCode.OK, listado.StatusCode);

        var response = await PostAsync($"{Base}/{solicitud.Id}/reenviar-invitacion", cookie);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var respuesta = await response.Content.ReadFromJsonAsync<ReenviarInvitacionResponse>();
        Assert.True(respuesta!.EmailEnviado);

        var invitaciones = await Coleccion<Invitacion>(MongoCollections.Invitaciones)
            .Find(FilterDefinition<Invitacion>.Empty).ToListAsync();
        Assert.Equal(2, invitaciones.Count);
        var revocada = invitaciones.Single(i => i.Id == anterior.Id);
        var nueva = invitaciones.Single(i => i.Id == respuesta.InvitacionId);
        Assert.Equal(EstadoInvitacion.Revocada, revocada.Estado);
        Assert.NotNull(revocada.RevocadaAt);
        Assert.Equal(EstadoInvitacion.Pendiente, nueva.Estado);
        Assert.NotEqual(revocada.TokenHash, nueva.TokenHash);
        Assert.True(nueva.ExpiraAt > DateTime.UtcNow.AddHours(47));
        Assert.Equal(_soporteId, nueva.CreadaPorAdministradorId);

        var email = Assert.Single(_emails.Enviados);
        Assert.Equal(PlantillasEmail.NombreInvitacionReenviada, email.Plantilla);
        var token = email.CuerpoTexto.Split("token=")[1].Split('\n')[0].Trim();
        Assert.Equal(nueva.TokenHash, TokenInvitacion.CalcularHash(Uri.UnescapeDataString(token)));
        Assert.Contains(await EventosDeAsync(solicitud.Id), e => e.Accion == "invitacion.reenviada" && e.AdministradorId == _soporteId);
    }

    [MongoFact]
    public async Task Reenviar_ConEmailCaido_DevuelveEmailNoEnviado()
    {
        var solicitud = await SembrarSolicitudAsync();
        var cookie = await LoginAsync(SuperAdminEmail);
        Assert.Equal(HttpStatusCode.OK, (await PostAsync($"{Base}/{solicitud.Id}/aprobar", cookie)).StatusCode);
        _emails.Excepcion = new InvalidOperationException("SMTP caído");

        var response = await PostAsync($"{Base}/{solicitud.Id}/reenviar-invitacion", cookie);

        await AssertErrorAsync(response, HttpStatusCode.BadGateway, "EMAIL_NO_ENVIADO");
        Assert.Single(
            await Coleccion<Invitacion>(MongoCollections.Invitaciones)
                .Find(i => i.Estado == EstadoInvitacion.Pendiente).ToListAsync());
    }

    [MongoFact]
    public async Task Reenviar_SolicitudPendiente_DevuelveInvitacionNoEncontrada()
    {
        var solicitud = await SembrarSolicitudAsync();
        var cookie = await LoginAsync(SoporteEmail);

        var response = await PostAsync($"{Base}/{solicitud.Id}/reenviar-invitacion", cookie);

        await AssertErrorAsync(response, HttpStatusCode.NotFound, "INVITACION_NO_ENCONTRADA");
    }

    [MongoFact]
    public async Task Listar_FiltraPorEstadoCuitRazonSocialYEmail()
    {
        await SembrarSolicitudAsync();
        await SembrarSolicitudAsync(OtroCuitValido, "otra@empresa.com", "Beta Servicios SRL", EstadoSolicitud.Rechazada);
        var cookie = await LoginAsync(SoporteEmail);

        var porEstado = await ListarAsync(cookie, "?estado=Pendiente");
        Assert.Equal(1, porEstado.Total);
        Assert.Equal(CuitValido, Assert.Single(porEstado.Items).Cuit);

        var porCuit = await ListarAsync(cookie, $"?cuit=30-71234567-1");
        Assert.Equal("Rechazada", Assert.Single(porCuit.Items).Estado);

        var porRazonSocial = await ListarAsync(cookie, "?razonSocial=beta");
        Assert.Equal(OtroCuitValido, Assert.Single(porRazonSocial.Items).Cuit);

        var porEmail = await ListarAsync(cookie, "?email=SOLICITANTE");
        Assert.Equal(EmailSolicitante, Assert.Single(porEmail.Items).Email);

        var paginado = await ListarAsync(cookie, "?page=2&pageSize=1");
        Assert.Equal(2, paginado.Total);
        Assert.Equal(2, paginado.TotalPages);
        Assert.Single(paginado.Items);

        await AssertErrorAsync(
            await SendAsync(HttpMethod.Get, $"{Base}?estado=Borrador", cookie),
            HttpStatusCode.BadRequest,
            "VALIDACION");
    }

    [MongoFact]
    public async Task Editar_SolicitudPendiente_ActualizaYDevuelveElDetalle()
    {
        var solicitud = await SembrarSolicitudAsync();
        var cookie = await LoginAsync(SuperAdminEmail);

        var response = await SendAsync(HttpMethod.Put, $"{Base}/{solicitud.Id}", cookie, Edicion("Nueva Razón SA"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detalle = await response.Content.ReadFromJsonAsync<SolicitudAltaDetalleResponse>();
        Assert.Equal("Nueva Razón SA", detalle!.RazonSocial);
        Assert.Equal((int)CondicionIva.Monotributo, detalle.CondicionIva);
        Assert.Equal("nuevo@demo.com", detalle.Email);
        Assert.Equal(CuitValido, detalle.Cuit);
        Assert.Equal("Pendiente", detalle.Estado);
        Assert.Contains(detalle.Historial, e => e.Accion == "solicitud.editada" && e.AdministradorId == _superAdminId);

        var guardada = await ObtenerSolicitudAsync(solicitud.Id);
        Assert.Equal("Nueva Razón SA", guardada.RazonSocial);
        Assert.Equal("+54 381 555-1234", guardada.Telefono);
        Assert.Equal("Calle Falsa 123", guardada.Domicilio);
        Assert.True(guardada.UpdatedAt > solicitud.UpdatedAt);
    }

    [MongoFact]
    public async Task Editar_SolicitudAprobada_DevuelveConflicto()
    {
        var solicitud = await SembrarSolicitudAsync(estado: EstadoSolicitud.Aprobada);
        var cookie = await LoginAsync(SuperAdminEmail);

        var response = await SendAsync(HttpMethod.Put, $"{Base}/{solicitud.Id}", cookie, Edicion("Nueva Razón SA"));

        await AssertErrorAsync(response, HttpStatusCode.Conflict, "SOLICITUD_YA_PROCESADA");
        Assert.Equal("ACME S.A.", (await ObtenerSolicitudAsync(solicitud.Id)).RazonSocial);
    }

    [MongoFact]
    public async Task Detalle_MuestraDatosInvitacionVigenteEHistorial()
    {
        var solicitud = await SembrarSolicitudAsync();
        var cookie = await LoginAsync(SuperAdminEmail);
        Assert.Equal(HttpStatusCode.OK, (await PostAsync($"{Base}/{solicitud.Id}/aprobar", cookie)).StatusCode);

        var response = await SendAsync(HttpMethod.Get, $"{Base}/{solicitud.Id}", await LoginAsync(SoporteEmail));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detalle = await response.Content.ReadFromJsonAsync<SolicitudAltaDetalleResponse>();
        Assert.Equal("Aprobada", detalle!.Estado);
        Assert.Equal("Ana Pérez", detalle.NombreSolicitante);
        Assert.NotNull(detalle.Invitacion);
        Assert.Equal(8, detalle.Invitacion.TokenPrefix.Length);
        Assert.Contains(detalle.Historial, e => e.Accion == "solicitud.aprobada");
        Assert.DoesNotContain("tokenHash", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [MongoFact]
    public async Task SinSesionDeAdministrador_DevuelveNoAutorizado()
    {
        var response = await _client!.GetAsync(Base);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static EditarSolicitudRequest Edicion(string razonSocial)
        => new(razonSocial, (int)CondicionIva.Monotributo, "Nuevo@Demo.com", "+54 381 555-1234", "Calle Falsa 123");

    private async Task<PaginaResponse<SolicitudAltaResumenResponse>> ListarAsync(string cookie, string query)
    {
        var response = await SendAsync(HttpMethod.Get, Base + query, cookie);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PaginaResponse<SolicitudAltaResumenResponse>>())!;
    }

    private async Task<Guid> SembrarAdministradorAsync(string email, RolGlobal rol)
    {
        var administrador = new AdministradorFacturar
        {
            Nombre = rol.ToString(),
            Email = email,
            PasswordHash = _hasher.Hash(Password),
            Rol = rol,
            Activo = true
        };
        await Coleccion<AdministradorFacturar>(MongoCollections.Administradores).InsertOneAsync(administrador);
        return administrador.Id;
    }

    private async Task<SolicitudAltaEmpresa> SembrarSolicitudAsync(
        string cuit = CuitValido,
        string email = EmailSolicitante,
        string razonSocial = "ACME S.A.",
        EstadoSolicitud estado = EstadoSolicitud.Pendiente)
    {
        var ahora = DateTime.UtcNow.AddMinutes(-5);
        var solicitud = new SolicitudAltaEmpresa
        {
            RazonSocial = razonSocial,
            Cuit = cuit,
            CondicionIva = CondicionIva.ResponsableInscripto,
            NombreSolicitante = "Ana Pérez",
            EmailSolicitante = email,
            Telefono = "3815551234",
            Domicilio = "San Martín 100",
            Ciudad = "Tucumán",
            Provincia = "Tucumán",
            CodigoPostal = "4000",
            AceptaTerminos = true,
            TerminosAceptadosAt = ahora,
            Estado = estado,
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

    private Task<HttpResponseMessage> PostAsync(string ruta, string cookie, object? body = null)
        => SendAsync(HttpMethod.Post, ruta, cookie, body);

    private async Task<HttpResponseMessage> SendAsync(HttpMethod metodo, string ruta, string cookie, object? body = null)
    {
        using var request = new HttpRequestMessage(metodo, ruta);
        request.Headers.TryAddWithoutValidation("Cookie", cookie);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, body.GetType());
        }

        return await _client!.SendAsync(request);
    }

    private static async Task AssertErrorAsync(HttpResponseMessage response, HttpStatusCode status, string codigo)
    {
        Assert.Equal(status, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal(codigo, error!.Codigo);
    }

    private Task<SolicitudAltaEmpresa> ObtenerSolicitudAsync(Guid id)
        => Coleccion<SolicitudAltaEmpresa>(MongoCollections.SolicitudesAlta).Find(s => s.Id == id).SingleAsync();

    private Task<List<EventoAuditoria>> EventosDeAsync(Guid solicitudId)
        => Coleccion<EventoAuditoria>(MongoCollections.Eventos)
            .Find(e => e.EntidadId == solicitudId.ToString())
            .ToListAsync();

    private Task<long> ContarAsync<T>(string coleccion)
        => Coleccion<T>(coleccion).CountDocumentsAsync(FilterDefinition<T>.Empty);

    private IMongoCollection<T> Coleccion<T>(string nombre)
        => _mongoClient!.GetDatabase(_databaseName).GetCollection<T>(nombre);
}
