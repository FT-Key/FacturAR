namespace FacturAR.Application.ApiKeys;

public sealed class ApiKeysOptions
{
    public const string SectionName = "ApiKeys";

    public string PrefijoFormato { get; set; } = "fak_";
    public int LongitudPrefijoHex { get; set; } = 12;
    public int BytesSecreto { get; set; } = 32;
    public int MaxIntentosGeneracionPrefijo { get; set; } = 5;
}
