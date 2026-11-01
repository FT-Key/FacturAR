namespace FacturAR.Application.Seed;

public sealed record SeedResult(bool Ejecutado, string? ApiKeyDemoValor, string? Error)
{
    public bool ApiKeyDemoCreada => ApiKeyDemoValor is not null;

    public static SeedResult Omitido() => new(false, null, null);

    public static SeedResult Invalido(string error) => new(false, null, error);

    public static SeedResult Completado(string? apiKeyDemoValor) => new(true, apiKeyDemoValor, null);
}
