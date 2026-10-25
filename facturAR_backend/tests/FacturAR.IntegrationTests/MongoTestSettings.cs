namespace FacturAR.IntegrationTests;

internal static class MongoTestSettings
{
    public static string? ConnectionString =>
        Environment.GetEnvironmentVariable("MONGO_CONNECTION_STRING")
        ?? Environment.GetEnvironmentVariable("Mongo__ConnectionString");

    public static string GetConnectionStringWithTimeout()
    {
        var connectionString = ConnectionString
            ?? throw new InvalidOperationException("Falta la connection string de MongoDB para el test.");

        if (connectionString.Contains("serverSelectionTimeoutMS", StringComparison.OrdinalIgnoreCase))
        {
            return connectionString;
        }

        var separator = connectionString.Contains('?') ? '&' : '?';
        return $"{connectionString}{separator}serverSelectionTimeoutMS=5000";
    }
}
