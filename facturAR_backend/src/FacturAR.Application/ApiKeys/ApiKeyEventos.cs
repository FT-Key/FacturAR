using System.Text.Json;
using FacturAR.Domain.Entities;

namespace FacturAR.Application.ApiKeys;

internal static class ApiKeyEventos
{
    public static string PrefijoVisible(string prefix, ApiKeysOptions options)
        => $"{options.PrefijoFormato}{prefix}";

    public static EventoAuditoria Evento(string accion, ApiKey apiKey, Guid usuarioId, DateTime ahora)
        => new()
        {
            Accion = accion,
            Entidad = nameof(ApiKey),
            EntidadId = apiKey.Id.ToString(),
            ContribuyenteId = apiKey.ContribuyenteId,
            UsuarioId = usuarioId,
            ActorTipo = "Empresa",
            Datos = JsonSerializer.Serialize(new Dictionary<string, string>
            {
                ["nombre"] = apiKey.Nombre,
                ["prefix"] = apiKey.Prefix
            }),
            CreatedAt = ahora
        };
}
