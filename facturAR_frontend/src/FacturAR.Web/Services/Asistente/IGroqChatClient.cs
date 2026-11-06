namespace FacturAR.Web.Services.Asistente;

public sealed record GroqMensaje(string Rol, string Contenido);

public sealed record GroqRespuesta(string Texto, int TokensUsados);

public interface IGroqChatClient
{
    Task<GroqRespuesta> ResponderAsync(IReadOnlyList<GroqMensaje> mensajes, CancellationToken cancellationToken);
}
