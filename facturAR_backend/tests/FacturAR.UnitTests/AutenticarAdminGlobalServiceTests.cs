using FacturAR.Application.Abstractions;
using FacturAR.Application.Auth;
using FacturAR.Domain.Entities;
using FacturAR.Domain.Enums;
using FacturAR.Infrastructure.LoginIntent;
using FacturAR.Infrastructure.Password;

namespace FacturAR.UnitTests;

public sealed class AutenticarAdminGlobalServiceTests
{
    private const string Password = "SuperSecreta123!";

    private readonly PasswordHasher _hasher = new(1000);
    private readonly LoginIntentStore _intentos = new(5, TimeSpan.FromMinutes(15));

    [Fact]
    public async Task ConCredencialesValidas_DevuelveLaSesionGlobal()
    {
        var administrador = AdministradorActivo();
        var service = CrearServicio(administrador);

        var sesion = await service.AutenticarAsync("  Root@Facturar.ar ", Password);

        Assert.NotNull(sesion);
        Assert.Equal(administrador.Id, sesion.AdministradorId);
        Assert.Equal("SuperAdmin", sesion.RolGlobal);
        Assert.Equal("Root FacturAR", sesion.Nombre);
        Assert.True(sesion.ExpiraAtUtc > DateTime.UtcNow);
    }

    [Fact]
    public async Task ConPasswordIncorrecta_DevuelveNull()
    {
        var service = CrearServicio(AdministradorActivo());

        Assert.Null(await service.AutenticarAsync("root@facturar.ar", "OtraClave123!"));
    }

    [Fact]
    public async Task ConAdministradorInactivo_DevuelveNull()
    {
        var administrador = AdministradorActivo();
        administrador.Activo = false;
        var service = CrearServicio(administrador);

        Assert.Null(await service.AutenticarAsync("root@facturar.ar", Password));
    }

    [Fact]
    public async Task ConAdministradorInexistente_DevuelveNull()
    {
        var service = CrearServicio(null);

        Assert.Null(await service.AutenticarAsync("desconocido@facturar.ar", Password));
    }

    private AutenticarAdminGlobalService CrearServicio(AdministradorFacturar? administrador)
        => new(
            new FakeAdministradorRepository(administrador),
            _intentos,
            _hasher,
            new AuthOptions());

    private AdministradorFacturar AdministradorActivo() => new()
    {
        Nombre = "Root FacturAR",
        Email = "root@facturar.ar",
        PasswordHash = _hasher.Hash(Password),
        Rol = RolGlobal.SuperAdmin,
        Activo = true
    };

    private sealed class FakeAdministradorRepository(AdministradorFacturar? administrador) : IAdministradorRepository
    {
        public Task<AdministradorFacturar?> GetByEmailAsync(string email, CancellationToken ct = default)
            => Task.FromResult(administrador is not null && administrador.Email == email ? administrador : null);

        public Task<AdministradorFacturar?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(administrador is not null && administrador.Id == id ? administrador : null);

        public Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default)
            => Task.FromResult(administrador is not null && administrador.Email == email);

        public Task InsertAsync(AdministradorFacturar administrador, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task UpdateAsync(AdministradorFacturar administrador, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}