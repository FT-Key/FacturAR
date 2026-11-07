using System.Text.Json;

namespace FacturAR.Application.Outbox;

public static class PayloadEmailSolicitud
{
    private static readonly JsonSerializerOptions Opciones = new(JsonSerializerDefaults.Web);

    public static string Crear(Guid solicitudId)
        => JsonSerializer.Serialize(new Contenido(solicitudId), Opciones);

    public static Guid? Leer(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        try
        {
            var contenido = JsonSerializer.Deserialize<Contenido>(payload, Opciones);
            return contenido is null || contenido.SolicitudId == Guid.Empty
                ? null
                : contenido.SolicitudId;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record Contenido(Guid SolicitudId);
}
