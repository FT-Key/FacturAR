using FacturAR.Application.Abstractions;
using FacturAR.Application.Notificaciones;
using FacturAR.Contracts.Solicitudes;
using FacturAR.Domain.Entities;
using FacturAR.Domain.Enums;
using FacturAR.Domain.Exceptions;
using FacturAR.Domain.Validation;
using FacturAR.Domain.ValueObjects;

namespace FacturAR.Application.Solicitudes;

public sealed class AprobarSolicitudService
{
    public const string AdvertenciaEmail = "No se pudo enviar el email de invitación. Podés reenviarlo.";
    public const string AccionAprobada = "solicitud.aprobada";
    public const int NumeroPuntoVentaInicial = 1;
    public const string DescripcionPuntoVentaInicial = "Casa Central";

    private readonly ISolicitudAltaRepository _solicitudes;
    private readonly IContribuyenteRepository _contribuyentes;
    private readonly IPuntoVentaRepository _puntosVenta;
    private readonly IInvitacionRepository _invitaciones;
    private readonly IEventoAuditoriaRepository _eventos;
    private readonly ITransaccionMongo _transaccion;
    private readonly EmailService _email;
    private readonly NotificadorRevisionSolicitudes _notificador;

    public AprobarSolicitudService(
        ISolicitudAltaRepository solicitudes,
        IContribuyenteRepository contribuyentes,
        IPuntoVentaRepository puntosVenta,
        IInvitacionRepository invitaciones,
        IEventoAuditoriaRepository eventos,
        ITransaccionMongo transaccion,
        EmailService email,
        NotificadorRevisionSolicitudes notificador)
    {
        _solicitudes = solicitudes;
        _contribuyentes = contribuyentes;
        _puntosVenta = puntosVenta;
        _invitaciones = invitaciones;
        _eventos = eventos;
        _transaccion = transaccion;
        _email = email;
        _notificador = notificador;
    }

    public async Task<AprobarSolicitudResponse> AprobarAsync(
        Guid solicitudId,
        Guid administradorId,
        CancellationToken ct = default)
    {
        var solicitud = await _solicitudes.ObtenerPorIdAsync(solicitudId, ct)
            ?? throw new SolicitudNoEncontradaException();

        if (solicitud.Estado != EstadoSolicitud.Pendiente)
        {
            throw new SolicitudYaProcesadaException();
        }

        if (!CuitValidator.TryNormalize(solicitud.Cuit, out _))
        {
            throw new SolicitudAltaValidationException("El CUIT de la solicitud no es válido.");
        }

        var token = TokenInvitacion.Generar(DateTime.UtcNow);

        var (aprobada, contribuyenteId, invitacionId) = await AprobarMapeandoCarreraAsync(
            solicitudId,
            administradorId,
            token,
            ct);

        var emailEnviado = await _notificador.EnviarAsync(
            PlantillasEmail.NombreSolicitudAprobada,
            aprobada,
            administradorId,
            invitacionId,
            () => _email.EnviarSolicitudAprobadaAsync(aprobada, token.Token, token.ExpiraAt, ct),
            ct);

        return new AprobarSolicitudResponse(
            contribuyenteId,
            invitacionId,
            emailEnviado,
            emailEnviado ? null : AdvertenciaEmail);
    }

    private async Task<(SolicitudAltaEmpresa Solicitud, Guid ContribuyenteId, Guid InvitacionId)> AprobarMapeandoCarreraAsync(
        Guid solicitudId,
        Guid administradorId,
        TokenInvitacion token,
        CancellationToken ct)
    {
        try
        {
            return await _transaccion.EjecutarAsync(
                innerCt => AprobarEnTransaccionAsync(solicitudId, administradorId, token, innerCt),
                ct);
        }
        catch (CuitYaRegistradoException)
        {
            var actual = await _solicitudes.ObtenerPorIdAsync(solicitudId, ct);
            if (actual is not null && actual.Estado != EstadoSolicitud.Pendiente)
            {
                throw new SolicitudYaProcesadaException();
            }

            throw;
        }
    }

    private async Task<(SolicitudAltaEmpresa Solicitud, Guid ContribuyenteId, Guid InvitacionId)> AprobarEnTransaccionAsync(
        Guid solicitudId,
        Guid administradorId,
        TokenInvitacion token,
        CancellationToken ct)
    {
        var solicitud = await _solicitudes.ObtenerPorIdAsync(solicitudId, ct)
            ?? throw new SolicitudNoEncontradaException();

        if (solicitud.Estado != EstadoSolicitud.Pendiente)
        {
            throw new SolicitudYaProcesadaException();
        }

        var cuit = CuitValidator.Normalize(solicitud.Cuit);
        if (await _contribuyentes.ExistePorCuitAsync(cuit, ct))
        {
            throw new CuitYaRegistradoException();
        }

        var ahora = DateTime.UtcNow;

        var contribuyente = new Contribuyente
        {
            Cuit = cuit,
            RazonSocial = solicitud.RazonSocial,
            CondicionIva = solicitud.CondicionIva,
            Email = solicitud.EmailSolicitante,
            Activo = true,
            CreatedAt = ahora
        };
        await _contribuyentes.InsertarAsync(contribuyente, ct);

        await _puntosVenta.InsertarAsync(
            new PuntoVenta
            {
                ContribuyenteId = contribuyente.Id,
                Numero = NumeroPuntoVentaInicial,
                Descripcion = DescripcionPuntoVentaInicial,
                Activo = true,
                CreatedAt = ahora
            },
            ct);

        var invitacion = new Invitacion
        {
            ContribuyenteId = contribuyente.Id,
            SolicitudId = solicitud.Id,
            Email = solicitud.EmailSolicitante,
            Rol = RolUsuario.Administrador,
            TokenHash = token.Hash,
            TokenPrefix = token.Prefix,
            Estado = EstadoInvitacion.Pendiente,
            ExpiraAt = token.ExpiraAt,
            CreadaPorAdministradorId = administradorId,
            IntentosEnvio = 0,
            CreatedAt = ahora
        };
        await _invitaciones.InsertarAsync(invitacion, ct);

        if (!await _solicitudes.AprobarPendienteAsync(
                solicitud.Id,
                contribuyente.Id,
                invitacion.Id,
                administradorId,
                ahora,
                ct))
        {
            throw new SolicitudYaProcesadaException();
        }

        solicitud.Estado = EstadoSolicitud.Aprobada;
        solicitud.ContribuyenteId = contribuyente.Id;
        solicitud.InvitacionId = invitacion.Id;
        solicitud.RevisadaPorAdministradorId = administradorId;
        solicitud.RevisadaAt = ahora;
        solicitud.UpdatedAt = ahora;

        await _eventos.AddAsync(
            NotificadorRevisionSolicitudes.Evento(
                AccionAprobada,
                solicitud,
                administradorId,
                new Dictionary<string, string>
                {
                    ["contribuyenteId"] = contribuyente.Id.ToString(),
                    ["invitacionId"] = invitacion.Id.ToString()
                }),
            ct);

        return (solicitud, contribuyente.Id, invitacion.Id);
    }
}
