using FacturAR.Application.Abstractions;
using FacturAR.Application.Auth;
using FacturAR.Domain.Entities;
using FacturAR.Domain.Enums;
using FacturAR.Infrastructure.LoginIntent;
using FacturAR.Infrastructure.Password;

namespace FacturAR.UnitTests;

public sealed class AutenticarEmpresaServiceTests
{
    private const string Password = "Secreta123!";

    private readonly PasswordHasher _hasher = new(1000);
    private readonly LoginIntentStore _intentos = new(5, TimeSpan.FromMinutes(15));

    [Fact]
    public async Task ConCredencialesValidas_DevuelveLaSesionDeLaEmpresa()
    {
        var contribuyente = ContribuyenteActivo();
        var usuario = UsuarioActivo(contribuyente.Id);
        var service = CrearServicio(usuario, contribuyente);

        var sesion = await service.AutenticarAsync("  Admin@Demo.com ", Password);

        Assert.NotNull(sesion);
        Assert.Equal(usuario.Id, sesion.UsuarioId);
        Assert.Equal("Administrador", sesion.Rol);
        Assert.Equal(contribuyente.Id, sesion.ContribuyenteId);
        Assert.Equal("Demo SRL", sesion.ContribuyenteRazonSocial);
        Assert.True(sesion.ExpiraAtUtc > DateTime.UtcNow);
    }

    [Fact]
    public async Task ConPasswordIncorrecta_DevuelveNullYRegistraElFallo()
    {
        var contribuyente = ContribuyenteActivo();
        var usuario = UsuarioActivo(contribuyente.Id);
        var service = CrearServicio(usuario, contribuyente);

        var sesion = await service.AutenticarAsync("admin@demo.com", "OtraClave123!");

        Assert.Null(sesion);
        Assert.Equal(1, _intentos.IntentosFallidos("admin@demo.com"));
    }

    [Fact]
    public async Task ConUsuarioInactivo_DevuelveNull()
    {
        var contribuyente = ContribuyenteActivo();
        var usuario = UsuarioActivo(contribuyente.Id);
        usuario.Activo = false;
        var service = CrearServicio(usuario, contribuyente);

        Assert.Null(await service.AutenticarAsync("admin@demo.com", Password));
    }

    [Fact]
    public async Task ConContribuyenteInactivo_DevuelveNull()
    {
        var contribuyente = ContribuyenteActivo();
        contribuyente.Activo = false;
        var usuario = UsuarioActivo(contribuyente.Id);
        var service = CrearServicio(usuario, contribuyente);

        Assert.Null(await service.AutenticarAsync("admin@demo.com", Password));
    }

    [Fact]
    public async Task ConUsuarioInexistente_DevuelveNull()
    {
        var contribuyente = ContribuyenteActivo();
        var service = CrearServicio(null, contribuyente);

        Assert.Null(await service.AutenticarAsync("desconocido@demo.com", Password));
    }

    [Fact]
    public async Task ConElEmailBloqueado_DevuelveNullAunqueLaPasswordSeaCorrecta()
    {
        var contribuyente = ContribuyenteActivo();
        var usuario = UsuarioActivo(contribuyente.Id);
        var service = CrearServicio(usuario, contribuyente);

        for (var i = 0; i < 5; i++)
        {
            await service.AutenticarAsync("admin@demo.com", "Incorrecta123!");
        }

        Assert.Null(await service.AutenticarAsync("admin@demo.com", Password));
    }

    private AutenticarEmpresaService CrearServicio(Usuario? usuario, Contribuyente contribuyente)
        => new(
            new FakeUsuarioRepository(usuario),
            new FakeContribuyenteRepository(contribuyente),
            _intentos,
            _hasher,
            new AuthOptions());

    private Contribuyente ContribuyenteActivo() => new()
    {
        RazonSocial = "Demo SRL",
        Cuit = "20123456786",
        Activo = true
    };

    private Usuario UsuarioActivo(Guid contribuyenteId) => new()
    {
        ContribuyenteId = contribuyenteId,
        Email = "admin@demo.com",
        PasswordHash = _hasher.Hash(Password),
        Rol = RolUsuario.Administrador,
        Activo = true
    };

    private sealed class FakeUsuarioRepository(Usuario? usuario) : IUsuarioRepository
    {
        public Task<Usuario?> GetByEmailAsync(string email, CancellationToken ct = default)
            => Task.FromResult(usuario is not null && usuario.Email == email ? usuario : null);

        public Task<Usuario?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(usuario is not null && usuario.Id == id ? usuario : null);

        public Task InsertAsync(Usuario usuario, CancellationToken ct = default) => Task.CompletedTask;

        public Task UpdateAsync(Usuario usuario, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeContribuyenteRepository(Contribuyente contribuyente) : IContribuyenteRepository
    {
        public Task<Contribuyente?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<Contribuyente?>(contribuyente.Id == id ? contribuyente : null);
    }
}