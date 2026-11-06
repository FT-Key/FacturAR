namespace FacturAR.Web.Services;

public static class EmisionIdempotencia
{
    public static string GenerarClave() => Guid.NewGuid().ToString("N");

    public static bool DebeRegenerar(string firmaAnterior, string firmaActual)
        => !string.Equals(firmaAnterior, firmaActual, StringComparison.Ordinal);
}
