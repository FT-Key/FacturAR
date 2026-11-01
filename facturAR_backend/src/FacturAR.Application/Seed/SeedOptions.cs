namespace FacturAR.Application.Seed;

public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    public bool Enabled { get; set; }

    public string Cuit { get; set; } = "20123456786";

    public string RazonSocial { get; set; } = "Empresa Demo (simulada)";

    public string Email { get; set; } = "admin@demo.facturar.local";

    public string Nombre { get; set; } = "Administrador Demo";

    public string? Password { get; set; }

    public bool ApiKeyDemo { get; set; }
}
