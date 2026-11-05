namespace FacturAR.Api.Soap;

public sealed class SoapOptions
{
    public const string SectionName = "Soap";

    public bool Habilitado { get; set; } = true;

    public string Ruta { get; set; } = "/soap";
}
