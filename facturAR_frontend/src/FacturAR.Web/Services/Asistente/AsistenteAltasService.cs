using System.Diagnostics;
using Microsoft.Extensions.Options;

namespace FacturAR.Web.Services.Asistente;

public sealed class AsistenteAltasService
{
    private readonly IGroqChatClient _groq;
    private readonly AsistenteLimites _limites;
    private readonly AsistenteOptions _options;
    private readonly ILogger<AsistenteAltasService> _logger;

    public AsistenteAltasService(
        IGroqChatClient groq,
        AsistenteLimites limites,
        IOptions<AsistenteOptions> options,
        ILogger<AsistenteAltasService> logger)
    {
        _groq = groq;
        _limites = limites;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AsistenteRespuesta> ResponderAsync(
        AsistenteMensajeRequest solicitud,
        string ip,
        string sesion,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        _limites.Verificar(ip, sesion, cancellationToken);
        var inicio = Stopwatch.GetTimestamp();

        try
        {
            var evaluacion = AsistentePolitica.EvaluarEntrada(solicitud.Mensaje, solicitud.Historial, _options);

            if (!evaluacion.Permitido)
            {
                if (evaluacion.Categoria == AsistentePolitica.CategoriaValidacion)
                {
                    throw new AsistenteValidacionException(evaluacion.Respuesta ?? "La solicitud no es válida.");
                }

                _logger.LogInformation("Asistente rechazó una consulta. Categoria: {Categoria}", evaluacion.Categoria);
                return new AsistenteRespuesta(
                    evaluacion.Respuesta ?? AsistentePolitica.MensajeRechazo,
                    AsistentePolitica.CategoriaFueraDeAlcance,
                    AsistentePolitica.Sugerencias(AsistentePolitica.CategoriaFueraDeAlcance));
            }

            var mensaje = AsistentePolitica.LimpiarMensaje(solicitud.Mensaje);
            var categoria = AsistentePolitica.Clasificar(mensaje);
            var mensajes = ConstruirMensajes(solicitud, mensaje);

            GroqRespuesta respuesta;
            try
            {
                respuesta = await _groq.ResponderAsync(mensajes, cancellationToken);
            }
            catch (AsistenteNoDisponibleException)
            {
                _limites.RegistrarResultado(false);
                throw;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _limites.RegistrarResultado(false);
                _logger.LogWarning(ex, "El proveedor del asistente falló.");
                throw new AsistenteNoDisponibleException("El asistente no está disponible.", ex);
            }

            _limites.RegistrarResultado(true);
            _limites.RegistrarUso(respuesta.TokensUsados);

            var duracion = Stopwatch.GetElapsedTime(inicio);
            _logger.LogInformation(
                "Asistente respondió. Categoria: {Categoria}. DuracionMs: {DuracionMs}. Tokens: {Tokens}.",
                categoria,
                (long)duracion.TotalMilliseconds,
                respuesta.TokensUsados);

            return new AsistenteRespuesta(
                AsistentePolitica.FiltrarSalida(respuesta.Texto),
                categoria,
                AsistentePolitica.Sugerencias(categoria));
        }
        finally
        {
            _limites.LiberarEnVuelo(ip);
        }
    }

    private IReadOnlyList<GroqMensaje> ConstruirMensajes(AsistenteMensajeRequest solicitud, string mensaje)
    {
        var mensajes = new List<GroqMensaje>(_options.MaxTurnosHistorial + 2)
        {
            new("system", AsistentePolitica.SystemPrompt)
        };

        if (solicitud.Historial is not null)
        {
            foreach (var turno in solicitud.Historial.TakeLast(_options.MaxTurnosHistorial))
            {
                var rol = turno.Rol!.Trim().ToLowerInvariant();
                var texto = AsistentePolitica.LimpiarMensaje(turno.Texto);
                if (texto.Length == 0)
                {
                    continue;
                }

                mensajes.Add(new GroqMensaje(rol, texto));
            }
        }

        mensajes.Add(new GroqMensaje("user", mensaje));
        return mensajes;
    }
}
