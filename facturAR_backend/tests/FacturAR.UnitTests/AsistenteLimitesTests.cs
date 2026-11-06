using FacturAR.Web.Services.Asistente;
using Microsoft.Extensions.Options;

namespace FacturAR.UnitTests;

public sealed class AsistenteLimitesTests
{
    private const string Ip = "203.0.113.10";
    private const string Sesion = "sesion-de-prueba-1234567890abcdef";

    [Fact]
    public void Verificar_SeptimoMensajeEnUnMinuto_LanzaLimiteConRetryAfter()
    {
        var reloj = new RelojFalso();
        var limites = Crear(reloj, new AsistenteOptions
        {
            RatePorMinutoPorIp = 6,
            RatePorHoraPorIp = 100,
            RatePorDiaPorIp = 100,
            RatePorHoraPorSesion = 100
        });

        for (var i = 0; i < 6; i++)
        {
            limites.Verificar(Ip, Sesion, CancellationToken.None);
            limites.LiberarEnVuelo(Ip);
        }

        var ex = Assert.Throws<AsistenteLimiteException>(
            () => limites.Verificar(Ip, Sesion, CancellationToken.None));

        Assert.True(ex.RetryAfter > TimeSpan.Zero);
    }

    [Fact]
    public void Verificar_ConUnRequestEnVuelo_RechazaElSegundo()
    {
        var limites = Crear(new RelojFalso(), new AsistenteOptions());

        limites.Verificar(Ip, Sesion, CancellationToken.None);

        Assert.Throws<AsistenteLimiteException>(
            () => limites.Verificar(Ip, Sesion, CancellationToken.None));
    }

    [Fact]
    public void Verificar_VigésimoPrimeroEnLaHoraPorSesion_LanzaLimite()
    {
        var limites = Crear(new RelojFalso(), new AsistenteOptions
        {
            RatePorMinutoPorIp = 100,
            RatePorHoraPorIp = 100,
            RatePorDiaPorIp = 100,
            RatePorHoraPorSesion = 20
        });

        for (var i = 0; i < 20; i++)
        {
            limites.Verificar(Ip, Sesion, CancellationToken.None);
            limites.LiberarEnVuelo(Ip);
        }

        var ex = Assert.Throws<AsistenteLimiteException>(
            () => limites.Verificar(Ip, Sesion, CancellationToken.None));

        Assert.True(ex.RetryAfter > TimeSpan.Zero);
    }

    [Fact]
    public void Verificar_PresupuestoDiarioDeTokensAgotado_LanzaNoDisponible()
    {
        var limites = Crear(new RelojFalso(), new AsistenteOptions
        {
            PresupuestoDiarioTokens = 100
        });

        limites.RegistrarUso(100);

        Assert.Throws<AsistenteNoDisponibleException>(
            () => limites.Verificar(Ip, Sesion, CancellationToken.None));
    }

    [Fact]
    public void Verificar_LimiteDeRequestsDiarios_LanzaNoDisponible()
    {
        var limites = Crear(new RelojFalso(), new AsistenteOptions
        {
            MaxRequestsPorDia = 2
        });

        limites.Verificar(Ip, Sesion, CancellationToken.None);
        limites.LiberarEnVuelo(Ip);
        limites.Verificar(Ip, Sesion, CancellationToken.None);
        limites.LiberarEnVuelo(Ip);

        Assert.Throws<AsistenteNoDisponibleException>(
            () => limites.Verificar(Ip, Sesion, CancellationToken.None));
    }

    [Fact]
    public void Verificar_AlCambiarElDia_ReiniciaElPresupuesto()
    {
        var reloj = new RelojFalso();
        var limites = Crear(reloj, new AsistenteOptions { PresupuestoDiarioTokens = 100 });

        limites.RegistrarUso(100);
        Assert.Throws<AsistenteNoDisponibleException>(
            () => limites.Verificar(Ip, Sesion, CancellationToken.None));

        reloj.Avanzar(TimeSpan.FromDays(1));

        limites.Verificar(Ip, Sesion, CancellationToken.None);
        limites.LiberarEnVuelo(Ip);

        Assert.Equal(0, limites.TokensConsumidosHoy);
    }

    [Fact]
    public void RegistrarResultado_CincoFallosConsecutivos_AbreElCircuitoYSeCierraAlAvanzarElReloj()
    {
        var reloj = new RelojFalso();
        var limites = Crear(reloj, new AsistenteOptions());

        for (var i = 0; i < 5; i++)
        {
            limites.RegistrarResultado(false);
        }

        Assert.True(limites.CircuitoAbierto);
        Assert.Throws<AsistenteNoDisponibleException>(
            () => limites.Verificar(Ip, Sesion, CancellationToken.None));

        reloj.Avanzar(TimeSpan.FromSeconds(61));

        Assert.False(limites.CircuitoAbierto);
        limites.Verificar(Ip, Sesion, CancellationToken.None);
        limites.LiberarEnVuelo(Ip);
    }

    [Fact]
    public void RegistrarResultado_ExitoIntermedio_NoAbreElCircuito()
    {
        var limites = Crear(new RelojFalso(), new AsistenteOptions());

        for (var i = 0; i < 4; i++)
        {
            limites.RegistrarResultado(false);
        }

        limites.RegistrarResultado(true);

        for (var i = 0; i < 4; i++)
        {
            limites.RegistrarResultado(false);
        }

        Assert.False(limites.CircuitoAbierto);
    }

    private static AsistenteLimites Crear(RelojFalso reloj, AsistenteOptions opciones)
        => new(Options.Create(opciones), reloj);

    private sealed class RelojFalso : TimeProvider
    {
        private DateTimeOffset _ahora = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _ahora;

        public void Avanzar(TimeSpan intervalo) => _ahora = _ahora.Add(intervalo);
    }
}
