using Xunit;

namespace FacturAR.IntegrationTests;

[AttributeUsage(AttributeTargets.Method)]
public sealed class MongoFactAttribute : FactAttribute
{
    public MongoFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(MongoTestSettings.ConnectionString))
        {
            Skip = "Configurar MONGO_CONNECTION_STRING (o Mongo__ConnectionString) para ejecutar los tests con MongoDB.";
        }
    }
}
