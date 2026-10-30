namespace FacturAR.Application.ApiKeys;

public sealed class ApiKeysOptions
{
    public const string SectionName = "ApiKeys";

    public string PrefijoFormato { get; set; } = "fak_";
    public int LongitudPrefijoHex { get; set; } = 12;
    public int BytesSecreto { get; set; } = 32;
    public int MaxIntentosGeneracionPrefijo { get; set; } = 5;
    public string HeaderName { get; set; } = "X-Api-Key";
    public bool ActualizarUltimoUso { get; set; } = true;
    public int IntervaloActualizacionUltimoUsoMinutos { get; set; } = 1;
    public int TimeoutActualizacionUltimoUsoMs { get; set; } = 1000;
}
