using FacturAR.Application.Abstractions;
using FacturAR.Contracts.Solicitudes;
using FacturAR.Domain.Enums;
using FacturAR.Domain.Exceptions;

namespace FacturAR.Application.Solicitudes;

public sealed class EditarSolicitudService
{
    public const string AccionEditada = "solicitud.editada";

    private readonly ISolicitudAltaRepository _solicitudes;
    private readonly IEventoAuditoriaRepository _eventos;
    private readonly ITransaccionMongo _transaccion;

    public EditarSolicitudService(
        ISolicitudAltaRepository solicitudes,
        IEventoAuditoriaRepository eventos,
        ITransaccionMongo transaccion)
    {
        _solicitudes = solicitudes;
        _eventos = eventos;
        _transaccion = transaccion;
    }

    public async Task EditarAsync(
        Guid solicitudId,
        EditarSolicitudRequest request,
        Guid administradorId,
        CancellationToken ct = default)
    {
        var razonSocial = (request.RazonSocial ?? string.Empty).Trim();
        if (razonSocial.Length is < 3 or > 200)
        {
            throw new SolicitudAltaValidationException("La razón social debe tener entre 3 y 200 caracteres.");
        }

        if (!Enum.IsDefined(typeof(CondicionIva), request.CondicionIva))
        {
            throw new SolicitudAltaValidationException("La condición frente al IVA no es válida.");
        }

        var email = (request.Email ?? string.Empty).Trim().ToLowerInvariant();
        if (!SolicitudAltaValidator.EsEmailValido(email))
        {
            throw new SolicitudAltaValidationException("El email no es válido.");
        }

        var telefono = (request.Telefono ?? string.Empty).Trim();
        if (telefono.Length is < 8 or > 32 || !telefono.All(SolicitudAltaValidator.EsCaracterDeTelefono))
        {
            throw new SolicitudAltaValidationException("El teléfono no es válido.");
        }

        var domicilio = (request.Domicilio ?? string.Empty).Trim();
        if (domicilio.Length is < 5 or > 200)
        {
            throw new SolicitudAltaValidationException("El domicilio es obligatorio.");
        }

        var solicitud = await _solicitudes.ObtenerPorIdAsync(solicitudId, ct)
            ?? throw new SolicitudNoEncontradaException();

        if (solicitud.Estado != EstadoSolicitud.Pendiente)
        {
            throw new SolicitudYaProcesadaException();
        }

        var condicionIva = (CondicionIva)request.CondicionIva;
        var campos = new List<string>();
        if (solicitud.RazonSocial != razonSocial)
        {
            campos.Add("razonSocial");
        }

        if (solicitud.CondicionIva != condicionIva)
        {
            campos.Add("condicionIva");
        }

        if (solicitud.EmailSolicitante != email)
        {
            campos.Add("email");
        }

        if (solicitud.Telefono != telefono)
        {
            campos.Add("telefono");
        }

        if (solicitud.Domicilio != domicilio)
        {
            campos.Add("domicilio");
        }

        solicitud.RazonSocial = razonSocial;
        solicitud.CondicionIva = condicionIva;
        solicitud.EmailSolicitante = email;
        solicitud.Telefono = telefono;
        solicitud.Domicilio = domicilio;
        solicitud.UpdatedAt = DateTime.UtcNow;

        await _transaccion.EjecutarAsync(
            async innerCt =>
            {
                if (!await _solicitudes.ActualizarDatosPendienteAsync(solicitud, innerCt))
                {
                    throw new SolicitudYaProcesadaException();
                }

                await _eventos.AddAsync(
                    NotificadorRevisionSolicitudes.Evento(
                        AccionEditada,
                        solicitud,
                        administradorId,
                        new Dictionary<string, string> { ["campos"] = string.Join(",", campos) }),
                    innerCt);

                return true;
            },
            ct);
    }
}
