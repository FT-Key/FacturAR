using System.ComponentModel.DataAnnotations;

namespace FacturAR.Web.Validation;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class CuitValidoAttribute : ValidationAttribute
{
    private static readonly int[] Pesos = [5, 4, 3, 2, 7, 6, 5, 4, 3, 2];

    public CuitValidoAttribute()
        : base("El CUIT no es válido.")
    {
    }

    public override bool IsValid(object? value)
    {
        var cuit = new string((value as string ?? string.Empty).Where(char.IsAsciiDigit).ToArray());
        if (cuit.Length != 11)
        {
            return true;
        }

        var suma = 0;
        for (var i = 0; i < Pesos.Length; i++)
        {
            suma += (cuit[i] - '0') * Pesos[i];
        }

        var resto = suma % 11;
        var verificador = resto switch
        {
            0 => 0,
            1 => 9,
            _ => 11 - resto
        };

        return cuit[10] - '0' == verificador;
    }
}
