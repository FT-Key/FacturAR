namespace FacturAR.Domain.Entities;

public sealed class ApiKey
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ContribuyenteId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Prefix { get; set; } = string.Empty;
    public string Hash { get; set; } = string.Empty;
    public bool Activa { get; set; } = true;
    public DateTime? RevocadaAt { get; set; }
    public DateTime? RegeneradaAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UltimoUso { get; set; }
}
