namespace FacturAR.Infrastructure.Outbox;

public sealed class OutboxBackoff
{
    private const double JitterPredeterminado = 0.2;

    private readonly Func<double> _aleatorio;

    public OutboxBackoff()
        : this(Random.Shared.NextDouble)
    {
    }

    public OutboxBackoff(Func<double> aleatorio)
    {
        _aleatorio = aleatorio;
    }

    public TimeSpan Calcular(int intentos, int intervaloBaseSegundos, double jitterFactor = JitterPredeterminado)
    {
        var baseSegundos = Math.Max(1, intervaloBaseSegundos);
        var exponente = Math.Max(0, intentos - 1);
        var segundos = baseSegundos * Math.Pow(2, exponente);

        var variacion = _aleatorio() * Math.Clamp(jitterFactor, 0, 1);
        var conJitter = segundos * (1 + variacion);

        return TimeSpan.FromSeconds(Math.Max(0, conJitter));
    }
}
