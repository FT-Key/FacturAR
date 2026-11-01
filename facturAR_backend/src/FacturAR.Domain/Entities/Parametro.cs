namespace FacturAR.Domain.Entities;

public sealed class Parametro
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? ContribuyenteId { get; set; }
    public string Clave { get; set; } = string.Empty;
    public string Valor { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
