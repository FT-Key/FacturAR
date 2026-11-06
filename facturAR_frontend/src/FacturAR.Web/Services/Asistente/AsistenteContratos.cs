using System.Text.Json.Serialization;

namespace FacturAR.Web.Services.Asistente;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AsistenteMensajeRequest(string? Mensaje, IReadOnlyList<AsistenteTurnoRequest>? Historial);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record AsistenteTurnoRequest(string? Rol, string? Texto);

public sealed record AsistenteRespuesta(string Respuesta, string Categoria, IReadOnlyList<string> Sugerencias);

public sealed record AsistenteErrorResponse(string Codigo, string Mensaje);

public sealed class AsistenteNoDisponibleException : Exception
{
    public AsistenteNoDisponibleException(string mensaje)
        : base(mensaje)
    {
    }

    public AsistenteNoDisponibleException(string mensaje, Exception innerException)
        : base(mensaje, innerException)
    {
    }
}

public sealed class AsistenteLimiteException : Exception
{
    public AsistenteLimiteException(TimeSpan retryAfter)
        : base("Se alcanzó el límite de mensajes del asistente.")
    {
        RetryAfter = retryAfter;
    }

    public TimeSpan RetryAfter { get; }
}

public sealed class AsistenteValidacionException : Exception
{
    public AsistenteValidacionException(string mensaje)
        : base(mensaje)
    {
    }
}
