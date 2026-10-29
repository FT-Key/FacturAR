using System.Collections.Concurrent;
using FacturAR.Application.Abstractions;

namespace FacturAR.Infrastructure.LoginIntent;

public sealed class LoginIntentStore : ILoginIntentStore
{
    private readonly ConcurrentDictionary<string, LoginAttempt> _intentos = new(StringComparer.Ordinal);
    private readonly int _maxIntentosFallidos;
    private readonly TimeSpan _bloqueo;

    public LoginIntentStore(int maxIntentosFallidos = 5, TimeSpan? bloqueo = null)
    {
        _maxIntentosFallidos = maxIntentosFallidos > 0 ? maxIntentosFallidos : 5;
        _bloqueo = bloqueo ?? TimeSpan.FromMinutes(15);
    }

    public Task RegistrarFalloAsync(string email, CancellationToken ct = default)
    {
        var clave = Normalizar(email);
        var ahora = DateTime.UtcNow;

        _intentos.AddOrUpdate(
            clave,
            _ => CrearConFallo(ahora),
            (_, actual) => Acumular(actual, ahora));

        return Task.CompletedTask;
    }

    public Task<bool> EstaBloqueadoAsync(string email, CancellationToken ct = default)
    {
        var clave = Normalizar(email);

        if (!_intentos.TryGetValue(clave, out var intento))
        {
            return Task.FromResult(false);
        }

        if (intento.BloqueadoHasta <= DateTime.MinValue)
        {
            return Task.FromResult(false);
        }

        if (intento.BloqueadoHasta > DateTime.UtcNow)
        {
            return Task.FromResult(true);
        }

        _intentos.TryRemove(clave, out _);
        return Task.FromResult(false);
    }

    public Task RegistrarExitoAsync(string email, CancellationToken ct = default)
    {
        _intentos.TryRemove(Normalizar(email), out _);
        return Task.CompletedTask;
    }

    public int IntentosFallidos(string email)
        => _intentos.TryGetValue(Normalizar(email), out var intento) ? intento.Fallos : 0;

    private LoginAttempt CrearConFallo(DateTime ahora)
        => new()
        {
            Fallos = 1,
            BloqueadoHasta = _maxIntentosFallidos <= 1 ? ahora + _bloqueo : DateTime.MinValue
        };

    private LoginAttempt Acumular(LoginAttempt actual, DateTime ahora)
    {
        var fallos = actual.Fallos + 1;

        if (fallos >= _maxIntentosFallidos)
        {
            return new LoginAttempt { Fallos = 0, BloqueadoHasta = ahora + _bloqueo };
        }

        return new LoginAttempt { Fallos = fallos, BloqueadoHasta = actual.BloqueadoHasta };
    }

    private static string Normalizar(string email) => email.Trim().ToLowerInvariant();

    private sealed class LoginAttempt
    {
        public int Fallos { get; init; }
        public DateTime BloqueadoHasta { get; init; } = DateTime.MinValue;
    }
}