using MongoDB.Driver;

namespace FacturAR.Infrastructure.Mongo;

internal static class MongoErrores
{
    private const int CodigoClaveDuplicada = 11000;
    private const string CampoIndice = "index";

    public static bool EsClaveDuplicada(Exception ex)
        => ex switch
        {
            MongoWriteException write => write.WriteError?.Code == CodigoClaveDuplicada,
            MongoCommandException command => command.Code == CodigoClaveDuplicada,
            MongoBulkWriteException bulk => TieneErrorDuplicado(bulk),
            _ => false
        };

    public static bool EsClaveDuplicadaEnIndice(Exception ex, string nombreIndice)
        => ex switch
        {
            MongoWriteException write => EsErrorDuplicadoEnIndice(write.WriteError, nombreIndice, ex.Message),
            MongoCommandException command => command.Code == CodigoClaveDuplicada
                && MensajeMencionaIndice(command.Message, nombreIndice),
            MongoBulkWriteException bulk => bulk.WriteErrors?.Any(error =>
                EsErrorDuplicadoEnIndice(error, nombreIndice, bulk.Message)) == true,
            _ => false
        };

    private static bool TieneErrorDuplicado(MongoBulkWriteException bulk)
        => bulk.WriteErrors?.Any(error => error.Code == CodigoClaveDuplicada) == true;

    private static bool EsErrorDuplicadoEnIndice(WriteError? error, string nombreIndice, string mensajeExcepcion)
    {
        if (error?.Code != CodigoClaveDuplicada)
        {
            return false;
        }

        if (error.Details is not null
            && error.Details.TryGetValue(CampoIndice, out var indice)
            && indice.IsString)
        {
            return string.Equals(indice.AsString, nombreIndice, StringComparison.OrdinalIgnoreCase);
        }

        return MensajeMencionaIndice(error.Message, nombreIndice)
            || MensajeMencionaIndice(mensajeExcepcion, nombreIndice);
    }

    private static bool MensajeMencionaIndice(string? mensaje, string nombreIndice)
        => !string.IsNullOrEmpty(mensaje)
            && mensaje.Contains(nombreIndice, StringComparison.OrdinalIgnoreCase);
}
