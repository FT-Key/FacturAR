namespace FacturAR.Infrastructure.Notificaciones;

internal static class EmailMasker
{
    public static string Enmascarar(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return "***";
        }

        var arroba = email.IndexOf('@');
        if (arroba <= 0 || arroba == email.Length - 1)
        {
            return "***";
        }

        var usuario = email[..arroba];
        var dominio = email[(arroba + 1)..];
        return $"{usuario[..1]}***@{dominio}";
    }
}
