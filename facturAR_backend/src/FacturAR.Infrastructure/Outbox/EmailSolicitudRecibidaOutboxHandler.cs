using FacturAR.Application.Abstractions;
using FacturAR.Application.Notificaciones;
using FacturAR.Application.Outbox;
using Microsoft.Extensions.Logging;

namespace FacturAR.Infrastructure.Outbox;

public sealed class EmailSolicitudRecibidaOutboxHandler : EmailSolicitudOutboxHandler
{
    public const string TipoMensaje = TiposMensajeOutbox.EmailSolicitudRecibida;

    public EmailSolicitudRecibidaOutboxHandler(
        ISolicitudAltaRepository solicitudes,
        EmailService email,
        IEventoAuditoriaRepository eventos,
        ILogger<EmailSolicitudRecibidaOutboxHandler> logger)
        : base(
            TipoMensaje,
            PlantillasEmail.NombreSolicitudRecibida,
            email.EnviarSolicitudRecibidaAsync,
            solicitudes,
            eventos,
            logger)
    {
    }
}
