using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace FacturAR.Infrastructure.Mongo;

public static class MongoSerialization
{
    private static readonly object Gate = new();
    private static bool s_configured;

    /// <summary>
    /// Debe ejecutarse antes de crear cualquier <see cref="MongoDB.Driver.MongoClient"/>.
    /// El registro de serializadores de MongoDB es global al proceso y no admite
    /// reemplazar un serializador de Guid ya registrado.
    /// </summary>
    public static void Configure()
    {
        if (s_configured)
        {
            return;
        }

        lock (Gate)
        {
            if (s_configured)
            {
                return;
            }

            BsonSerializer.TryRegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
            s_configured = true;
        }
    }
}