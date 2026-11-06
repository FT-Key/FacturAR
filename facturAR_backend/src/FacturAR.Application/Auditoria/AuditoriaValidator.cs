using System.Text.Json;
using FacturAR.Application.Abstractions;
using FacturAR.Domain;
using FacturAR.Domain.Entities;

namespace FacturAR.Application.Auditoria;

public static class AuditoriaValidator
{
    private static readonly string[] ClavesProhibidas =
    [
        "token",
        "hash",
        "password",
        "contrasena",
        "contraseña",
        "prefix",
        "prefijo",
        "apikey",
        "secret",
        "secreto"
    ];

    public static void Validar(ContextoAuditoria contexto, string? datos = null)
    {
        ValidarContexto(contexto);
        ValidarDatos(datos);
    }

    public static void Validar(EventoAuditoria evento)
    {
        ArgumentNullException.ThrowIfNull(evento);

        var contexto = new ContextoAuditoria(
            evento.ActorTipo,
            evento.ContribuyenteId,
            evento.UsuarioId,
            evento.AdministradorId);

        Validar(contexto, evento.Datos);
    }

    private static void ValidarContexto(ContextoAuditoria contexto)
    {
        if (contexto.ActorTipo == ActorTipos.Empresa)
        {
            if (contexto.ContribuyenteId is null || contexto.UsuarioId is null || contexto.AdministradorId is not null)
            {
                throw new ArgumentException(
                    "Un evento de empresa requiere ContribuyenteId y UsuarioId, y no admite AdministradorId.");
            }

            return;
        }

        if (contexto.ActorTipo == ActorTipos.Administrador)
        {
            if (contexto.AdministradorId is null || contexto.ContribuyenteId is not null || contexto.UsuarioId is not null)
            {
                throw new ArgumentException(
                    "Un evento de administrador requiere AdministradorId y no admite ContribuyenteId ni UsuarioId.");
            }

            return;
        }

        if (contexto.ActorTipo == ActorTipos.Sistema)
        {
            if (contexto.UsuarioId is not null || contexto.AdministradorId is not null)
            {
                throw new ArgumentException(
                    "Un evento de sistema no admite UsuarioId ni AdministradorId.");
            }

            return;
        }

        throw new ArgumentException("El tipo de actor del evento no es válido.");
    }

    private static void ValidarDatos(string? datos)
    {
        if (string.IsNullOrWhiteSpace(datos))
        {
            return;
        }

        JsonDocument documento;
        try
        {
            documento = JsonDocument.Parse(datos);
        }
        catch (JsonException)
        {
            throw new ArgumentException("Los datos del evento deben ser un JSON válido.");
        }

        using (documento)
        {
            if (ContieneClaveProhibida(documento.RootElement))
            {
                throw new ArgumentException("Los datos del evento contienen campos sensibles.");
            }
        }
    }

    private static bool ContieneClaveProhibida(JsonElement elemento)
    {
        switch (elemento.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var propiedad in elemento.EnumerateObject())
                {
                    if (EsClaveProhibida(propiedad.Name) || ContieneClaveProhibida(propiedad.Value))
                    {
                        return true;
                    }
                }

                return false;

            case JsonValueKind.Array:
                foreach (var item in elemento.EnumerateArray())
                {
                    if (ContieneClaveProhibida(item))
                    {
                        return true;
                    }
                }

                return false;

            default:
                return false;
        }
    }

    private static bool EsClaveProhibida(string nombre)
    {
        var normalizada = nombre
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();

        return ClavesProhibidas.Any(clave => normalizada.Contains(clave, StringComparison.Ordinal));
    }
}
