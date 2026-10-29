namespace FacturAR.Domain.Validation;

public static class CaeValidator
{
    private static readonly char[] Separadores = ['-', '.', '/', ' '];

    public static string Normalize(string? cae)
    {
        if (string.IsNullOrWhiteSpace(cae))
        {
            return string.Empty;
        }

        return new string(cae.Trim().Where(c => !Separadores.Contains(c)).ToArray());
    }

    public static bool IsValid(string? cae)
        => cae is { Length: 14 } && cae.All(char.IsAsciiDigit);
}
