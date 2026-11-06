using System.Text.Json;
using FacturAR.Application.Auditoria;
using FacturAR.Domain;
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
            Entidad = EntidadesAuditoria.ApiKey,
            EntidadId = apiKey.Id.ToString(),
            ContribuyenteId = apiKey.ContribuyenteId,
            UsuarioId = usuarioId,
            ActorTipo = ActorTipos.Empresa,
            Datos = JsonSerializer.Serialize(new Dictionary<string, string>
            {
                ["nombre"] = apiKey.Nombre
            }),
            CreatedAt = ahora
        };
}
