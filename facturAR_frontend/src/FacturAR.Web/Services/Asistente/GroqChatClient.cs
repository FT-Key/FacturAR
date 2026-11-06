using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace FacturAR.Web.Services.Asistente;

public sealed class GroqChatClient : IGroqChatClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly AsistenteOptions _options;

    public GroqChatClient(HttpClient httpClient, IOptions<AsistenteOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<GroqRespuesta> ResponderAsync(IReadOnlyList<GroqMensaje> mensajes, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.GroqApiKey))
        {
            throw new AsistenteNoDisponibleException("El asistente no tiene configurada la clave del proveedor.");
        }

        var solicitud = new
        {
            model = _options.Modelo,
            messages = mensajes.Select(mensaje => new { role = mensaje.Rol, content = mensaje.Contenido }).ToArray(),
            temperature = 0.2,
            max_tokens = _options.MaxTokensRespuesta
        };

        using var peticion = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl.TrimEnd('/')}/chat/completions");
        peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.GroqApiKey);
        peticion.Content = JsonContent.Create(solicitud, options: SerializerOptions);

        try
        {
            using var respuesta = await _httpClient.SendAsync(peticion, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!respuesta.IsSuccessStatusCode)
            {
                throw new AsistenteNoDisponibleException("El proveedor del asistente respondió con un error.");
            }

            var contenido = await respuesta.Content.ReadFromJsonAsync<GroqChatRespuesta>(SerializerOptions, cancellationToken);
            var texto = contenido?.Choices?.FirstOrDefault()?.Message?.Content;

            if (string.IsNullOrWhiteSpace(texto))
            {
                throw new AsistenteNoDisponibleException("El proveedor del asistente devolvió una respuesta vacía.");
            }

            return new GroqRespuesta(texto.Trim(), contenido?.Usage?.TotalTokens ?? 0);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AsistenteNoDisponibleException("El asistente tardó demasiado en responder.");
        }
        catch (HttpRequestException ex)
        {
            throw new AsistenteNoDisponibleException("No pudimos contactar al proveedor del asistente.", ex);
        }
        catch (JsonException ex)
        {
            throw new AsistenteNoDisponibleException("El proveedor del asistente devolvió una respuesta inválida.", ex);
        }
    }

    private sealed record GroqChatRespuesta(
        [property: JsonPropertyName("choices")] IReadOnlyList<GroqChoice>? Choices,
        [property: JsonPropertyName("usage")] GroqUsage? Usage);

    private sealed record GroqChoice(
        [property: JsonPropertyName("message")] GroqMensajeRespuesta? Message);

    private sealed record GroqMensajeRespuesta(
        [property: JsonPropertyName("content")] string? Content);

    private sealed record GroqUsage(
        [property: JsonPropertyName("total_tokens")] int? TotalTokens);
}
