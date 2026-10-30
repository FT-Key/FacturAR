using MongoDB.Driver;

namespace FacturAR.Infrastructure.Mongo;

internal static class MongoErrores
{
    private const int CodigoClaveDuplicada = 11000;

    public static bool EsClaveDuplicada(Exception ex)
        => ex switch
        {
            MongoWriteException write => write.WriteError?.Code == CodigoClaveDuplicada,
            MongoCommandException command => command.Code == CodigoClaveDuplicada,
            _ => false
        };
}
