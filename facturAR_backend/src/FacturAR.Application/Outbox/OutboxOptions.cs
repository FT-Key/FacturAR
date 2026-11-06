namespace FacturAR.Application.Outbox;

public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    public bool Habilitado { get; set; } = true;

    public int MaxIntentos { get; set; } = 5;

    public int IntervaloBaseSegundos { get; set; } = 5;

    public int IntervaloBarridoSegundos { get; set; } = 10;

    public int LoteTamano { get; set; } = 20;
}
