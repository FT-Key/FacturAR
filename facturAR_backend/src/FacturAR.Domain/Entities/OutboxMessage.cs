namespace FacturAR.Domain.Entities;

public sealed class OutboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Tipo { get; set; } = string.Empty;
    public string Payload { get; set; } = "{}";
    public string Estado { get; set; } = "Pendiente";
    public int Intentos { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcesadoAt { get; set; }
    public string? Error { get; set; }
}
