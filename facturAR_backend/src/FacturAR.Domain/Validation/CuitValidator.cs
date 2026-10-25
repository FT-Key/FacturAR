namespace FacturAR.Domain.Validation;

public static class CuitValidator
{
    private static readonly int[] Pesos = [5, 4, 3, 2, 7, 6, 5, 4, 3, 2];

    public static string Normalize(string? cuit)
        => new((cuit ?? string.Empty).Where(char.IsAsciiDigit).ToArray());

    public static bool IsValid(string? cuit)
    {
        var normalized = Normalize(cuit);
        if (normalized.Length != 11 || !normalized.All(char.IsAsciiDigit))
        {
            return false;
        }

        var suma = 0;
        for (var i = 0; i < Pesos.Length; i++)
        {
            suma += (normalized[i] - '0') * Pesos[i];
        }

        var resto = suma % 11;
        var verificador = resto switch
        {
            0 => 0,
            1 => 9,
            _ => 11 - resto
        };

        return normalized[10] - '0' == verificador;
    }

    public static bool TryNormalize(string? cuit, out string normalizado)
    {
        normalizado = Normalize(cuit);
        return IsValid(normalizado);
    }
}
