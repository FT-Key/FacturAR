using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using FacturAR.Application.ApiKeys;

namespace FacturAR.UnitTests;

public sealed class ApiKeyHasherTests
{
    private const string Valor = "fak_9f3a1c7b2e40_AbCdEfGhIjKlMnOpQrStUvWxYz0123456789-_AbC";

    private static readonly Regex FormatoHash = new(
        "^[0-9a-f]{64}$",
        RegexOptions.CultureInvariant);

    private readonly ApiKeyHasher _hasher = new();

    [Fact]
    public void Hash_DevuelveSha256HexadecimalEnMinusculasDeSesentaYCuatroCaracteres()
    {
        var hash = _hasher.Hash(Valor);

        Assert.Matches(FormatoHash, hash);
        Assert.Equal(64, hash.Length);
    }

    [Fact]
    public void Hash_EsDeterministico()
    {
        Assert.Equal(_hasher.Hash(Valor), _hasher.Hash(Valor));
    }

    [Fact]
    public void Hash_CoincideConSha256DelValorCompleto()
    {
        var esperado = Convert
            .ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Valor)))
            .ToLowerInvariant();

        Assert.Equal(esperado, _hasher.Hash(Valor));
    }

    [Theory]
    [InlineData("fak_9f3a1c7b2e40_AbCdEfGhIjKlMnOpQrStUvWxYz0123456789-_AbC")]
    public void Matches_ConElValorCorrecto_DevuelveTrue(string valor)
    {
        Assert.True(_hasher.Matches(valor, _hasher.Hash(valor)));
    }

    [Theory]
    [InlineData("fak_9f3a1c7b2e40_AbCdEfGhIjKlMnOpQrStUvWxYz0123456789-_AbD")]
    [InlineData("fak_9f3a1c7b2e40_AbCdEfGhIjKlMnOpQrStUvWxYz0123456789-_AbC ")]
    [InlineData("FAK_9f3a1c7b2e40_AbCdEfGhIjKlMnOpQrStUvWxYz0123456789-_AbC")]
    [InlineData("fak_9f3a1c7b2e41_AbCdEfGhIjKlMnOpQrStUvWxYz0123456789-_AbC")]
    public void Matches_ConUnaVariante_DevuelveFalse(string variante)
    {
        Assert.False(_hasher.Matches(variante, _hasher.Hash(Valor)));
    }

    [Fact]
    public void Matches_ConHashDeOtraLongitud_DevuelveFalseSinLanzarExcepcion()
    {
        Assert.False(_hasher.Matches(Valor, "abcd"));
    }

    [Fact]
    public void Matches_ConHashNoHexadecimal_DevuelveFalseSinLanzarExcepcion()
    {
        Assert.False(_hasher.Matches(Valor, new string('z', 64)));
    }

    [Theory]
    [InlineData("", "0123456789abcdef")]
    [InlineData("fak_9f3a1c7b2e40_secreto", "")]
    public void Matches_ConValorOHashVacio_DevuelveFalse(string valor, string hash)
    {
        Assert.False(_hasher.Matches(valor, hash));
    }
}
