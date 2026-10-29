using FacturAR.Infrastructure.LoginIntent;

namespace FacturAR.UnitTests;

public sealed class LoginIntentStoreTests
{
    [Fact]
    public async Task EstaBloqueado_SinIntentosPrevios_DevuelveFalse()
    {
        var store = new LoginIntentStore(2, TimeSpan.FromMinutes(15));

        Assert.False(await store.EstaBloqueadoAsync("usuario@demo.com"));
    }

    [Fact]
    public async Task AlAlcanzarElMaximoDeFallos_QuedaBloqueado()
    {
        var store = new LoginIntentStore(2, TimeSpan.FromMinutes(15));

        await store.RegistrarFalloAsync("usuario@demo.com");
        Assert.False(await store.EstaBloqueadoAsync("usuario@demo.com"));

        await store.RegistrarFalloAsync("usuario@demo.com");
        Assert.True(await store.EstaBloqueadoAsync("usuario@demo.com"));
    }

    [Fact]
    public async Task RegistrarExito_LimpiaElContadorYElBloqueo()
    {
        var store = new LoginIntentStore(2, TimeSpan.FromMinutes(15));

        await store.RegistrarFalloAsync("usuario@demo.com");
        await store.RegistrarFalloAsync("usuario@demo.com");
        await store.RegistrarExitoAsync("usuario@demo.com");

        Assert.False(await store.EstaBloqueadoAsync("usuario@demo.com"));
    }

    [Fact]
    public async Task ElBloqueoVencido_DesbloqueaAlUsuario()
    {
        var store = new LoginIntentStore(1, TimeSpan.Zero);

        await store.RegistrarFalloAsync("usuario@demo.com");

        Assert.False(await store.EstaBloqueadoAsync("usuario@demo.com"));
    }

    [Fact]
    public async Task ElEmailSeNormaliza_AntesDeContar()
    {
        var store = new LoginIntentStore(1, TimeSpan.FromMinutes(15));

        await store.RegistrarFalloAsync("  Usuario@Demo.com  ");

        Assert.True(await store.EstaBloqueadoAsync("usuario@demo.com"));
    }
}