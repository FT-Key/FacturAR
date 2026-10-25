using System.Security.Cryptography;

namespace FacturAR.Application.Comprobantes;

public static class CaeGenerator
{
    public static string Generar()
    {
        Span<char> digitos = stackalloc char[14];
        digitos[0] = (char)('0' + RandomNumberGenerator.GetInt32(1, 10));

        for (var i = 1; i < digitos.Length; i++)
        {
            digitos[i] = (char)('0' + RandomNumberGenerator.GetInt32(0, 10));
        }

        return new string(digitos);
    }

    public static DateTime CalcularVencimiento(DateTime fechaEmision)
        => fechaEmision.Date.AddDays(10);
}
