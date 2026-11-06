using Microsoft.Extensions.Options;

namespace FacturAR.Web.Services.Asistente;

public sealed class AsistenteLimites
{
    private const int FallosParaAbrirCircuito = 5;
    private static readonly TimeSpan DuracionCircuito = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan VentanaMinuto = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan VentanaHora = TimeSpan.FromHours(1);
    private static readonly TimeSpan VentanaDia = TimeSpan.FromDays(1);

    private readonly object _sincronizacion = new();
    private readonly AsistenteOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly Dictionary<string, Ventanas> _porIp = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Ventanas> _porSesion = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _enVuelo = new(StringComparer.Ordinal);

    private DateOnly _diaGlobal;
    private int _requestsGlobal;
    private long _tokensGlobal;
    private int _fallosConsecutivos;
    private DateTimeOffset _circuitoAbiertoHasta;
    private int _operaciones;

    public AsistenteLimites(IOptions<AsistenteOptions> options, TimeProvider timeProvider)
    {
        _options = options.Value;
        _timeProvider = timeProvider;
        _diaGlobal = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
    }

    public int TokensConsumidosHoy
    {
        get
        {
            lock (_sincronizacion)
            {
                return (int)Math.Min(int.MaxValue, _tokensGlobal);
            }
        }
    }

    public bool CircuitoAbierto
    {
        get
        {
            lock (_sincronizacion)
            {
                return _circuitoAbiertoHasta > _timeProvider.GetUtcNow();
            }
        }
    }

    public void Verificar(string ip, string sesion, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_sincronizacion)
        {
            var ahora = _timeProvider.GetUtcNow();

            if (_circuitoAbiertoHasta > ahora)
            {
                throw new AsistenteNoDisponibleException("El asistente está pausado por un problema temporal del proveedor.");
            }

            var diaActual = DateOnly.FromDateTime(ahora.UtcDateTime);
            if (diaActual != _diaGlobal)
            {
                _diaGlobal = diaActual;
                _requestsGlobal = 0;
                _tokensGlobal = 0;
            }

            if (_requestsGlobal >= _options.MaxRequestsPorDia || _tokensGlobal >= _options.PresupuestoDiarioTokens)
            {
                throw new AsistenteNoDisponibleException("El asistente alcanzó su presupuesto diario.");
            }

            if (_enVuelo.TryGetValue(ip, out var enVuelo) && enVuelo > 0)
            {
                throw new AsistenteLimiteException(TimeSpan.FromSeconds(1));
            }

            var ventanasIp = Obtener(_porIp, ip, ahora);
            var ventanasSesion = Obtener(_porSesion, sesion, ahora);

            var espera = Maximo(
                Evaluar(ventanasIp.Minuto, ahora, VentanaMinuto, _options.RatePorMinutoPorIp),
                Evaluar(ventanasIp.Hora, ahora, VentanaHora, _options.RatePorHoraPorIp),
                Evaluar(ventanasIp.Dia, ahora, VentanaDia, _options.RatePorDiaPorIp),
                Evaluar(ventanasSesion.Hora, ahora, VentanaHora, _options.RatePorHoraPorSesion));

            if (espera > TimeSpan.Zero)
            {
                throw new AsistenteLimiteException(espera);
            }

            ventanasIp.Minuto.Enqueue(ahora);
            ventanasIp.Hora.Enqueue(ahora);
            ventanasIp.Dia.Enqueue(ahora);
            ventanasSesion.Hora.Enqueue(ahora);
            _enVuelo[ip] = enVuelo + 1;
            _requestsGlobal++;

            _operaciones++;
            if (_operaciones % 512 == 0)
            {
                Limpiar(ahora);
            }
        }
    }

    public void RegistrarUso(int tokens)
    {
        if (tokens <= 0)
        {
            return;
        }

        lock (_sincronizacion)
        {
            _tokensGlobal += tokens;
        }
    }

    public void RegistrarResultado(bool exito)
    {
        lock (_sincronizacion)
        {
            if (exito)
            {
                _fallosConsecutivos = 0;
                return;
            }

            _fallosConsecutivos++;
            if (_fallosConsecutivos >= FallosParaAbrirCircuito)
            {
                _fallosConsecutivos = 0;
                _circuitoAbiertoHasta = _timeProvider.GetUtcNow().Add(DuracionCircuito);
            }
        }
    }

    public void LiberarEnVuelo(string ip)
    {
        lock (_sincronizacion)
        {
            if (!_enVuelo.TryGetValue(ip, out var enVuelo))
            {
                return;
            }

            if (enVuelo <= 1)
            {
                _enVuelo.Remove(ip);
                return;
            }

            _enVuelo[ip] = enVuelo - 1;
        }
    }

    private static TimeSpan Evaluar(Queue<DateTimeOffset> marcas, DateTimeOffset ahora, TimeSpan ventana, int limite)
    {
        while (marcas.Count > 0 && ahora - marcas.Peek() >= ventana)
        {
            marcas.Dequeue();
        }

        if (marcas.Count < limite)
        {
            return TimeSpan.Zero;
        }

        var espera = ventana - (ahora - marcas.Peek());
        return espera > TimeSpan.Zero ? espera : TimeSpan.FromSeconds(1);
    }

    private static TimeSpan Maximo(params TimeSpan[] valores)
    {
        var maximo = TimeSpan.Zero;
        foreach (var valor in valores)
        {
            if (valor > maximo)
            {
                maximo = valor;
            }
        }

        return maximo;
    }

    private void Limpiar(DateTimeOffset ahora)
    {
        LimpiarDiccionario(_porIp, ahora);
        LimpiarDiccionario(_porSesion, ahora);
    }

    private static void LimpiarDiccionario(Dictionary<string, Ventanas> diccionario, DateTimeOffset ahora)
    {
        List<string>? obsoletas = null;
        foreach (var par in diccionario)
        {
            if (ahora - par.Value.UltimoUso < VentanaDia)
            {
                continue;
            }

            obsoletas ??= [];
            obsoletas.Add(par.Key);
        }

        if (obsoletas is null)
        {
            return;
        }

        foreach (var clave in obsoletas)
        {
            diccionario.Remove(clave);
        }
    }

    private static Ventanas Obtener(Dictionary<string, Ventanas> diccionario, string clave, DateTimeOffset ahora)
    {
        if (!diccionario.TryGetValue(clave, out var ventanas))
        {
            ventanas = new Ventanas();
            diccionario[clave] = ventanas;
        }

        ventanas.UltimoUso = ahora;
        return ventanas;
    }

    private sealed class Ventanas
    {
        public Queue<DateTimeOffset> Minuto { get; } = new();

        public Queue<DateTimeOffset> Hora { get; } = new();

        public Queue<DateTimeOffset> Dia { get; } = new();

        public DateTimeOffset UltimoUso { get; set; }
    }
}
