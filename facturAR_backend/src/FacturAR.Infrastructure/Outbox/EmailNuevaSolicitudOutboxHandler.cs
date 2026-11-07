using FacturAR.Application.Abstractions;
using FacturAR.Application.Notificaciones;
using FacturAR.Application.Outbox;
using Microsoft.Extensions.Logging;

namespace FacturAR.Infrastructure.Outbox;

public sealed class EmailNuevaSolicitudOutboxHandler : EmailSolicitudOutboxHandler
{
    public const string TipoMensaje = TiposMensajeOutbox.EmailNuevaSolicitud;

    public EmailNuevaSolicitudOutboxHandler(
        ISolicitudAltaRepository solicitudes,
        EmailService email,
        IEventoAuditoriaRepository eventos,
        ILogger<EmailNuevaSolicitudOutboxHandler> logger)
        : base(
            TipoMensaje,
            PlantillasEmail.NombreNuevaSolicitud,
            email.EnviarNuevaSolicitudAsync,
            solicitudes,
            eventos,
            logger)
    {
    }
}
