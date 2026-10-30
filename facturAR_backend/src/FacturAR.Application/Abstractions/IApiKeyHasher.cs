namespace FacturAR.Application.Abstractions;

public interface IApiKeyHasher
{
    string Hash(string valor);

    bool Matches(string valor, string hash);
}
