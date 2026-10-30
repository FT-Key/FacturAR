using System.Net.Mail;
using FacturAR.Contracts.Solicitudes;
using FacturAR.Domain.Entities;
using FacturAR.Domain.Enums;
using FacturAR.Domain.Exceptions;
using FacturAR.Domain.Validation;

namespace FacturAR.Application.Solicitudes;

public static class SolicitudAltaValidator
{
    private const int EmailMaximo = 254;
    private const int MensajeMaximo = 1000;

    public static SolicitudAltaEmpresa Validar(SolicitudAltaRequest request)
    {
        var razonSocial = (request.RazonSocial ?? string.Empty).Trim();
        if (razonSocial.Length == 0)
        {
            throw new SolicitudAltaValidationException("La razón social es obligatoria.");
        }

        if (razonSocial.Length is < 3 or > 200)
        {
            throw new SolicitudAltaValidationException("La razón social debe tener entre 3 y 200 caracteres.");
        }

        if (!CuitValidator.TryNormalize(request.Cuit, out var cuit))
        {
            throw new SolicitudAltaValidationException("El CUIT no es válido.");
        }

        if (!Enum.IsDefined(typeof(CondicionIva), request.CondicionIva))
        {
            throw new SolicitudAltaValidationException("La condición frente al IVA no es válida.");
        }

        var nombreSolicitante = (request.NombreSolicitante ?? string.Empty).Trim();
        if (nombreSolicitante.Length is < 2 or > 120)
        {
            throw new SolicitudAltaValidationException("El nombre del solicitante es obligatorio.");
        }

        var email = (request.Email ?? string.Empty).Trim().ToLowerInvariant();
        if (!EsEmailValido(email))
        {
            throw new SolicitudAltaValidationException("El email no es válido.");
        }

        var telefono = (request.Telefono ?? string.Empty).Trim();
        if (telefono.Length is < 8 or > 32 || !telefono.All(EsCaracterDeTelefono))
        {
            throw new SolicitudAltaValidationException("El teléfono no es válido.");
        }

        var domicilio = (request.Domicilio ?? string.Empty).Trim();
        if (domicilio.Length is < 5 or > 200)
        {
            throw new SolicitudAltaValidationException("El domicilio es obligatorio.");
        }

        var ciudad = (request.Ciudad ?? string.Empty).Trim();
        if (ciudad.Length is < 2 or > 100)
        {
            throw new SolicitudAltaValidationException("La ciudad es obligatoria.");
        }

        var provincia = (request.Provincia ?? string.Empty).Trim();
        if (provincia.Length is < 2 or > 100)
        {
            throw new SolicitudAltaValidationException("La provincia es obligatoria.");
        }

        var codigoPostal = (request.CodigoPostal ?? string.Empty).Trim();
        if (codigoPostal.Length is < 4 or > 10 || !codigoPostal.All(char.IsLetterOrDigit))
        {
            throw new SolicitudAltaValidationException("El código postal no es válido.");
        }

        var mensaje = string.IsNullOrWhiteSpace(request.Mensaje) ? null : request.Mensaje.Trim();
        if (mensaje is { Length: > MensajeMaximo })
        {
            throw new SolicitudAltaValidationException("El mensaje es demasiado largo.");
        }

        if (!request.AceptaTerminos)
        {
            throw new SolicitudAltaValidationException("Tenés que aceptar los términos para enviar la solicitud.");
        }

        var ahora = DateTime.UtcNow;

        return new SolicitudAltaEmpresa
        {
            RazonSocial = razonSocial,
            Cuit = cuit,
            CondicionIva = (CondicionIva)request.CondicionIva,
            NombreSolicitante = nombreSolicitante,
            EmailSolicitante = email,
            Telefono = telefono,
            Domicilio = domicilio,
            Ciudad = ciudad,
            Provincia = provincia,
            CodigoPostal = codigoPostal,
            Mensaje = mensaje,
            AceptaTerminos = true,
            TerminosAceptadosAt = ahora,
            Estado = EstadoSolicitud.Pendiente,
            CreatedAt = ahora,
            UpdatedAt = ahora
        };
    }

    internal static bool EsEmailValido(string email)
        => email.Length is > 0 and <= EmailMaximo
            && MailAddress.TryCreate(email, out var direccion)
            && direccion is not null
            && string.Equals(direccion.Address, email, StringComparison.OrdinalIgnoreCase);

    internal static bool EsCaracterDeTelefono(char caracter)
        => char.IsAsciiDigit(caracter) || caracter is ' ' or '+' or '-' or '(' or ')';
}
