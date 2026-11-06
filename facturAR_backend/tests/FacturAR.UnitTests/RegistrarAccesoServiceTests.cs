using FacturAR.Application.Abstractions;
using FacturAR.Application.Auth;
using FacturAR.Application.Auditoria;
using FacturAR.Domain;
using FacturAR.Domain.Entities;

namespace FacturAR.UnitTests;

public sealed class RegistrarAccesoServiceTests
{
    private static readonly Guid ContribuyenteId = Guid.NewGuid();
    private static readonly Guid UsuarioId = Guid.NewGuid();
    private static readonly Guid AdministradorId = Guid.NewGuid();

    [Fact]
    public async Task AccesoDenegado_SinActores_RegistraSistemaSinActores()
    {
        var eventos = new EventosFake();
        var servicio = new RegistrarAccesoService(eventos);

        await servicio.RegistrarAccesoDenegadoAsync(null, null, null);

        var evento = Assert.Single(eventos.Eventos);
        Assert.Equal(AccionesAuditoria.AccesoDenegado, evento.Accion);
        Assert.Equal(EntidadesAuditoria.Recurso, evento.Entidad);
        Assert.Equal(ActorTipos.Sistema, evento.ActorTipo);
        Assert.Null(evento.ContribuyenteId);
        Assert.Null(evento.UsuarioId);
        Assert.Null(evento.AdministradorId);
    }

    [Fact]
    public async Task AccesoDenegado_ConUsuarioYContribuyente_RegistraEmpresa()
    {
        var eventos = new EventosFake();
        var servicio = new RegistrarAccesoService(eventos);

        await servicio.RegistrarAccesoDenegadoAsync(UsuarioId, ContribuyenteId, null);

        var evento = Assert.Single(eventos.Eventos);
        Assert.Equal(ActorTipos.Empresa, evento.ActorTipo);
        Assert.Equal(ContribuyenteId, evento.ContribuyenteId);
        Assert.Equal(UsuarioId, evento.UsuarioId);
        Assert.Null(evento.AdministradorId);
    }

    [Fact]
    public async Task AccesoDenegado_ConUsuarioSinContribuyente_LanzaArgumento()
    {
        var servicio = new RegistrarAccesoService(new EventosFake());

        await Assert.ThrowsAsync<ArgumentException>(
            () => servicio.RegistrarAccesoDenegadoAsync(UsuarioId, null, null));
    }

    [Fact]
    public async Task AccesoDenegado_ConAdministrador_RegistraAdministrador()
    {
        var eventos = new EventosFake();
        var servicio = new RegistrarAccesoService(eventos);

        await servicio.RegistrarAccesoDenegadoAsync(null, null, AdministradorId);

        var evento = Assert.Single(eventos.Eventos);
        Assert.Equal(ActorTipos.Administrador, evento.ActorTipo);
        Assert.Null(evento.ContribuyenteId);
        Assert.Null(evento.UsuarioId);
        Assert.Equal(AdministradorId, evento.AdministradorId);
    }

    [Fact]
    public async Task AdminGlobalCreado_RegistraSistemaSinAdministradorId()
    {
        var eventos = new EventosFake();
        var servicio = new RegistrarAccesoService(eventos);

        await servicio.RegistrarAdminGlobalCreadoAsync(AdministradorId);

        var evento = Assert.Single(eventos.Eventos);
        Assert.Equal(AccionesAuditoria.AdminGlobalCreado, evento.Accion);
        Assert.Equal(ActorTipos.Sistema, evento.ActorTipo);
        Assert.Equal(AdministradorId.ToString(), evento.EntidadId);
        Assert.Null(evento.AdministradorId);
    }

    [Fact]
    public async Task LoginFallido_RegistraSistemaSinActores()
    {
        var eventos = new EventosFake();
        var servicio = new RegistrarAccesoService(eventos);

        await servicio.RegistrarLoginFallidoAsync();

        var evento = Assert.Single(eventos.Eventos);
        Assert.Equal(AccionesAuditoria.LoginFallido, evento.Accion);
        Assert.Equal(ActorTipos.Sistema, evento.ActorTipo);
        Assert.Null(evento.UsuarioId);
        Assert.Null(evento.AdministradorId);
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
