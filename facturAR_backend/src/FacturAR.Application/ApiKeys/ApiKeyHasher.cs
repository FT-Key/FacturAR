using System.Security.Cryptography;
using System.Text;
using FacturAR.Application.Abstractions;

namespace FacturAR.Application.ApiKeys;

public sealed class ApiKeyHasher : IApiKeyHasher
{
    public string Hash(string valor)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(valor))).ToLowerInvariant();

    public bool Matches(string valor, string hash)
    {
        if (string.IsNullOrEmpty(valor) || string.IsNullOrEmpty(hash))
        {
            return false;
        }

        byte[] esperado;
        try
        {
            esperado = Convert.FromHexString(hash);
        }
        catch (FormatException)
        {
            return false;
        }

        var calculado = SHA256.HashData(Encoding.UTF8.GetBytes(valor));
        return CryptographicOperations.FixedTimeEquals(calculado, esperado);
    }
}
