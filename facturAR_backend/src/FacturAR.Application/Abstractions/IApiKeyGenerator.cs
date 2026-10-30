namespace FacturAR.Application.Abstractions;

public sealed record ApiKeyGeneratorValue(string Prefix, string Secret, string Valor);

public interface IApiKeyGenerator
{
    ApiKeyGeneratorValue Generar();
}
