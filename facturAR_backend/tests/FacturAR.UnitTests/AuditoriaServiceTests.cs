using FacturAR.Application.Abstractions;
using FacturAR.Application.Auditoria;
using FacturAR.Domain;
using FacturAR.Domain.Entities;
using FacturAR.Infrastructure.Auditoria;

namespace FacturAR.UnitTests;

public sealed class AuditoriaServiceTests
{
    private static readonly Guid ContribuyenteId = Guid.NewGuid();
    private static readonly Guid UsuarioId = Guid.NewGuid();
    private static readonly Guid AdministradorId = Guid.NewGuid();

    [Fact]
    public async Task Empresa_ConContribuyenteYUsuario_RegistraElEvento()
    {
        var contexto = new EventosFake();
        var servicio = new AuditoriaService(contexto);

        await servicio.RegistrarAsync(
            ContextoAuditoria.Empresa(ContribuyenteId, UsuarioId),
            "ApiKeyCreada",
            "ApiKey",
            "entidad-1",
            """{"nombre":"Ecommerce"}""");

        var evento = Assert.Single(contexto.Eventos);
        Assert.Equal("ApiKeyCreada", evento.Accion);
        Assert.Equal("ApiKey", evento.Entidad);
        Assert.Equal("entidad-1", evento.EntidadId);
        Assert.Equal(ActorTipos.Empresa, evento.ActorTipo);
        Assert.Equal(ContribuyenteId, evento.ContribuyenteId);
        Assert.Equal(UsuarioId, evento.UsuarioId);
        Assert.Null(evento.AdministradorId);
        Assert.Equal(DateTimeKind.Utc, evento.CreatedAt.Kind);
    }

    [Fact]
    public async Task Empresa_SinUsuario_LanzaArgumento()
    {
        var servicio = new AuditoriaService(new EventosFake());

        await Assert.ThrowsAsync<ArgumentException>(() => servicio.RegistrarAsync(
            new ContextoAuditoria(ActorTipos.Empresa, ContribuyenteId),
            "ApiKeyCreada",
            "ApiKey"));
    }

    [Fact]
    public async Task Empresa_ConAdministrador_LanzaArgumento()
    {
        var servicio = new AuditoriaService(new EventosFake());

        await Assert.ThrowsAsync<ArgumentException>(() => servicio.RegistrarAsync(
            new ContextoAuditoria(ActorTipos.Empresa, ContribuyenteId, UsuarioId, AdministradorId),
            "ApiKeyCreada",
            "ApiKey"));
    }

    [Fact]
    public async Task Administrador_Valido_RegistraSinContribuyente()
    {
        var contexto = new EventosFake();
        var servicio = new AuditoriaService(contexto);

        await servicio.RegistrarAsync(
            ContextoAuditoria.Administrador(AdministradorId),
            "SolicitudAprobada",
            "SolicitudAltaEmpresa");

        var evento = Assert.Single(contexto.Eventos);
        Assert.Equal(ActorTipos.Administrador, evento.ActorTipo);
        Assert.Equal(AdministradorId, evento.AdministradorId);
        Assert.Null(evento.ContribuyenteId);
        Assert.Null(evento.UsuarioId);
    }

    [Fact]
    public async Task Administrador_ConContribuyente_LanzaArgumento()
    {
        var servicio = new AuditoriaService(new EventosFake());

        await Assert.ThrowsAsync<ArgumentException>(() => servicio.RegistrarAsync(
            new ContextoAuditoria(ActorTipos.Administrador, ContribuyenteId, null, AdministradorId),
            "SolicitudAprobada",
            "SolicitudAltaEmpresa"));
    }

    [Fact]
    public async Task Sistema_ConContribuyenteAfectado_RegistraElEvento()
    {
        var contexto = new EventosFake();
        var servicio = new AuditoriaService(contexto);

        await servicio.RegistrarAsync(
            ContextoAuditoria.Sistema(ContribuyenteId),
            "OutboxProcesado",
            EntidadesAuditoria.OutboxMessage,
            Guid.NewGuid().ToString());

        var evento = Assert.Single(contexto.Eventos);
        Assert.Equal(ActorTipos.Sistema, evento.ActorTipo);
        Assert.Equal(ContribuyenteId, evento.ContribuyenteId);
        Assert.Null(evento.UsuarioId);
        Assert.Null(evento.AdministradorId);
    }

    [Fact]
    public async Task Sistema_ConUsuario_LanzaArgumento()
    {
        var servicio = new AuditoriaService(new EventosFake());

        await Assert.ThrowsAsync<ArgumentException>(() => servicio.RegistrarAsync(
            new ContextoAuditoria(ActorTipos.Sistema, null, UsuarioId),
            "OutboxProcesado",
            EntidadesAuditoria.OutboxMessage));
    }

    [Fact]
    public async Task ActorTipoDesconocido_LanzaArgumento()
    {
        var servicio = new AuditoriaService(new EventosFake());

        await Assert.ThrowsAsync<ArgumentException>(() => servicio.RegistrarAsync(
            new ContextoAuditoria("Visitante"),
            "SolicitudCreada",
            "SolicitudAltaEmpresa"));
    }

    [Fact]
    public async Task DatosConToken_LanzaArgumento()
    {
        var servicio = new AuditoriaService(new EventosFake());

        await Assert.ThrowsAsync<ArgumentException>(() => servicio.RegistrarAsync(
            ContextoAuditoria.Administrador(AdministradorId),
            "InvitacionEnviada",
            "Invitacion",
            datos: """{"token":"abc123"}"""));
    }

    [Fact]
    public async Task DatosConPrefijoDeApiKey_LanzaArgumento()
    {
        var servicio = new AuditoriaService(new EventosFake());

        await Assert.ThrowsAsync<ArgumentException>(() => servicio.RegistrarAsync(
            ContextoAuditoria.Empresa(ContribuyenteId, UsuarioId),
            "ApiKeyCreada",
            "ApiKey",
            datos: """{"prefix":"9f3a1c7b2e40"}"""));
    }

    [Fact]
    public async Task DatosConHashDePassword_LanzaArgumento()
    {
        var servicio = new AuditoriaService(new EventosFake());

        await Assert.ThrowsAsync<ArgumentException>(() => servicio.RegistrarAsync(
            ContextoAuditoria.Empresa(ContribuyenteId, UsuarioId),
            "CuentaActivada",
            "Usuario",
            datos: """{"passwordHash":"hash"}"""));
    }

    [Theory]
    [InlineData("""{"tokenHash":"abc123"}""")]
    [InlineData("""{"apiKeyPrefix":"9f3a1c7b2e40"}""")]
    [InlineData("""{"passwordHash":"hash"}""")]
    [InlineData("""{"token_hash":"abc123"}""")]
    [InlineData("""{"api-key-prefix":"9f3a1c7b2e40"}""")]
    public async Task DatosConClavesSensiblesNormalizadas_LanzaArgumento(string datos)
    {
        var servicio = new AuditoriaService(new EventosFake());

        await Assert.ThrowsAsync<ArgumentException>(() => servicio.RegistrarAsync(
            ContextoAuditoria.Empresa(ContribuyenteId, UsuarioId),
            "ApiKeyCreada",
            "ApiKey",
            datos: datos));
    }

    [Fact]
    public void ValidarEvento_ConEventoDeEmpresaSinUsuario_LanzaArgumento()
    {
        var evento = new EventoAuditoria
        {
            Accion = "ComprobanteEmitido",
            Entidad = "Comprobante",
            ActorTipo = ActorTipos.Empresa,
            ContribuyenteId = ContribuyenteId
        };

        Assert.Throws<ArgumentException>(() => AuditoriaValidator.Validar(evento));
    }

    [Fact]
    public void ValidarEvento_ConEventoDeSistemaValido_NoLanza()
    {
        var evento = new EventoAuditoria
        {
            Accion = "OutboxProcesado",
            Entidad = EntidadesAuditoria.OutboxMessage,
            ActorTipo = ActorTipos.Sistema
        };

        AuditoriaValidator.Validar(evento);
    }

    [Fact]
    public async Task DatosNoJson_LanzaArgumento()
    {
        var servicio = new AuditoriaService(new EventosFake());

        await Assert.ThrowsAsync<ArgumentException>(() => servicio.RegistrarAsync(
            ContextoAuditoria.Empresa(ContribuyenteId, UsuarioId),
            "ApiKeyCreada",
            "ApiKey",
            datos: "no-es-json"));
    }

    private sealed class EventosFake : IEventoAuditoriaRepository
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
}
