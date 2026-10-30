namespace FacturAR.Domain.Validation;

public static class PoliticaContrasena
{
    public const int LargoMinimo = 10;
    public const int LargoMaximo = 128;

    private static readonly HashSet<string> Triviales = new(StringComparer.OrdinalIgnoreCase)
    {
        "password123",
        "password1234",
        "contrasena1",
        "contrasena123",
        "contraseña1",
        "contraseña123",
        "qwerty12345",
        "qwerty123456",
        "admin12345",
        "admin123456",
        "123456789a",
        "a123456789",
        "facturar123",
        "facturar1234"
    };

    public static string? Validar(string? password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return "La contraseña es obligatoria.";
        }

        if (password.Length < LargoMinimo)
        {
            return $"La contraseña debe tener al menos {LargoMinimo} caracteres.";
        }

        if (password.Length > LargoMaximo)
        {
            return "La contraseña es demasiado larga.";
        }

        if (!password.Any(char.IsLetter))
        {
            return "La contraseña debe incluir al menos una letra.";
        }

        if (!password.Any(char.IsDigit))
        {
            return "La contraseña debe incluir al menos un número.";
        }

        if (EsTrivial(password))
        {
            return "La contraseña es demasiado predecible. Elegí otra.";
        }

        return null;
    }

    private static bool EsTrivial(string password)
    {
        if (Triviales.Contains(password))
        {
            return true;
        }

        var normalizada = password.ToLowerInvariant();
        if (normalizada.Distinct().Count() <= 2)
        {
            return true;
        }

        var ascendente = true;
        var descendente = true;
        for (var i = 1; i < normalizada.Length; i++)
        {
            var diferencia = normalizada[i] - normalizada[i - 1];
            ascendente &= diferencia == 1;
            descendente &= diferencia == -1;
        }

        return ascendente || descendente;
    }
}
