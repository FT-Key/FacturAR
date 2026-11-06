using FacturAR.Application.Abstractions;

namespace FacturAR.Infrastructure.Outbox;

public sealed class OutboxHandlerResolver
{
    private readonly Dictionary<string, IOutboxHandler> _handlers;

    public OutboxHandlerResolver(IEnumerable<IOutboxHandler> handlers)
    {
        _handlers = new Dictionary<string, IOutboxHandler>(StringComparer.Ordinal);

        foreach (var handler in handlers)
        {
            if (string.IsNullOrWhiteSpace(handler.Tipo))
            {
                throw new InvalidOperationException("Un handler de outbox no tiene tipo configurado.");
            }

            if (!_handlers.TryAdd(handler.Tipo, handler))
            {
                throw new InvalidOperationException(
                    $"Hay más de un handler de outbox registrado para el tipo {handler.Tipo}.");
            }
        }
    }

    public IOutboxHandler? Obtener(string tipo)
        => _handlers.GetValueOrDefault(tipo);
}
