using FacturAR.Infrastructure.Password;

namespace FacturAR.UnitTests;

public sealed class PasswordHasherTests
{
    [Fact]
    public void Hash_GeneraElFormatoAutodescriptivoEsperado()
    {
        var hasher = new PasswordHasher(1000);

        var hash = hasher.Hash("Secreta123!");

        var partes = hash.Split('$');
        Assert.Equal(4, partes.Length);
        Assert.Equal("pbkdf2-sha256", partes[0]);
        Assert.Equal("1000", partes[1]);
        Assert.False(string.IsNullOrWhiteSpace(partes[2]));
        Assert.False(string.IsNullOrWhiteSpace(partes[3]));
    }

    [Fact]
    public void Hash_UsaUnaSalDistintaEnCadaLlamada()
    {
        var hasher = new PasswordHasher(1000);

        var primero = hasher.Hash("Secreta123!");
        var segundo = hasher.Hash("Secreta123!");

        Assert.NotEqual(primero, segundo);
    }

    [Fact]
    public void Verify_ConLaPasswordCorrecta_DevuelveTrue()
    {
        var hasher = new PasswordHasher(1000);
        var hash = hasher.Hash("Secreta123!");

        Assert.True(hasher.Verify("Secreta123!", hash));
    }

    [Fact]
    public void Verify_ConUnaPasswordIncorrecta_DevuelveFalse()
    {
        var hasher = new PasswordHasher(1000);
        var hash = hasher.Hash("Secreta123!");

        Assert.False(hasher.Verify("OtraClave123!", hash));
    }

    [Theory]
    [InlineData("")]
    [InlineData("sin-formato")]
    [InlineData("pbkdf2-sha256$no-es-numero$c2FsdA$aGFzaA")]
    [InlineData("pbkdf2-sha256$1000$c2FsdA")]
    [InlineData("md5$1000$c2FsdA$aGFzaA")]
    [InlineData("pbkdf2-sha256$1000$***$aGFzaA")]
    public void Verify_ConUnHashMalformado_DevuelveFalse(string hash)
    {
        var hasher = new PasswordHasher(1000);

        Assert.False(hasher.Verify("Secreta123!", hash));
    }

    [Fact]
    public void NeedsRehash_ConIteracionesMenoresALaConfigurada_DevuelveTrue()
    {
        var hashViejo = new PasswordHasher(1000).Hash("Secreta123!");
        var hasher = new PasswordHasher(600000);

        Assert.True(hasher.NeedsRehash(hashViejo));
    }

    [Fact]
    public void NeedsRehash_ConLasMismasIteraciones_DevuelveFalse()
    {
        var hasher = new PasswordHasher(1000);
        var hash = hasher.Hash("Secreta123!");

        Assert.False(hasher.NeedsRehash(hash));
    }

    [Fact]
    public void NeedsRehash_ConUnHashMalformado_DevuelveTrue()
    {
        var hasher = new PasswordHasher(600000);

        Assert.True(hasher.NeedsRehash("sin-formato"));
    }

    [Fact]
    public void Hash_NoIncluyeLaPasswordEnClaro()
    {
        const string password = "Secreta123!";
        var hasher = new PasswordHasher(1000);

        var hash = hasher.Hash(password);

        Assert.DoesNotContain(password, hash, StringComparison.Ordinal);
    }
}