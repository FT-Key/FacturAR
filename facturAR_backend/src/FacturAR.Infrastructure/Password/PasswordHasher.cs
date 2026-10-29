using System.Security.Cryptography;
using FacturAR.Application.Abstractions;

namespace FacturAR.Infrastructure.Password;

public sealed class PasswordHasher : IPasswordHasher
{
    private const int DefaultIterations = 600000;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    private readonly int _iterations;

    public PasswordHasher(int iterations = DefaultIterations)
    {
        _iterations = iterations > 0 ? iterations : DefaultIterations;
    }

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Derive(password, salt, _iterations);

        return $"pbkdf2-sha256${_iterations}${ToBase64Url(salt)}${ToBase64Url(hash)}";
    }

    public bool Verify(string password, string hash)
    {
        var parts = hash.Split('$');
        if (parts.Length != 4 || !string.Equals(parts[0], "pbkdf2-sha256", StringComparison.Ordinal))
        {
            return false;
        }

        if (!int.TryParse(parts[1], out var iterations) || iterations <= 0)
        {
            return false;
        }

        byte[] salt;
        byte[] expected;
        try
        {
            salt = FromBase64Url(parts[2]);
            expected = FromBase64Url(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = Derive(password, salt, iterations);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    public bool NeedsRehash(string hash)
    {
        var parts = hash.Split('$');
        if (parts.Length != 4 || !string.Equals(parts[0], "pbkdf2-sha256", StringComparison.Ordinal))
        {
            return true;
        }

        return !int.TryParse(parts[1], out var iterations) || iterations < _iterations;
    }

    private static byte[] Derive(string password, byte[] salt, int iterations)
        => Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            HashSize);

    private static string ToBase64Url(byte[] value)
        => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromBase64Url(string value)
    {
        var normalized = value.Replace('-', '+').Replace('_', '/');
        normalized = (normalized.Length % 4) switch
        {
            2 => normalized + "==",
            3 => normalized + "=",
            _ => normalized
        };

        return Convert.FromBase64String(normalized);
    }
}