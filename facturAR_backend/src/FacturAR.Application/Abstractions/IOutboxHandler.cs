using FacturAR.Domain.Entities;

namespace FacturAR.Application.Abstractions;

public interface IOutboxHandler
{
    string Tipo { get; }

    Task HandleAsync(OutboxMessage mensaje, CancellationToken ct);
}
