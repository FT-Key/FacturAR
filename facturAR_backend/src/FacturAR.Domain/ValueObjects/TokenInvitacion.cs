using System.Security.Cryptography;
using System.Text;

namespace FacturAR.Domain.ValueObjects;

public sealed class TokenInvitacion
{
    public static readonly TimeSpan Vigencia = TimeSpan.FromHours(48);

    private const int BytesAleatorios = 32;
    private const int LargoPrefijo = 8;

    private TokenInvitacion(string token, DateTime expiraAt)
    {
        Token = token;
        Hash = CalcularHash(token);
        Prefix = token[..LargoPrefijo];
        ExpiraAt = expiraAt;
    }

    public string Token { get; }

    public string Hash { get; }

    public string Prefix { get; }

    public DateTime ExpiraAt { get; }

    public static TokenInvitacion Generar(DateTime ahora)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(BytesAleatorios))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        return new TokenInvitacion(token, ahora.Add(Vigencia));
    }

    public static string CalcularHash(string token)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public override string ToString() => $"{Prefix}…";
}
