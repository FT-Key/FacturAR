namespace FacturAR.Domain.Validation;

public static class CaeValidator
{
    public static bool IsValid(string? cae)
        => cae is { Length: 14 } && cae.All(char.IsAsciiDigit);
}
