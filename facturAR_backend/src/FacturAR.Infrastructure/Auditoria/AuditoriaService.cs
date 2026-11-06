using FacturAR.Application.Abstractions;
using FacturAR.Application.Auditoria;
using FacturAR.Domain.Entities;

namespace FacturAR.Infrastructure.Auditoria;

public sealed class AuditoriaService : IAuditoriaService
{
    private readonly IEventoAuditoriaRepository _eventos;

    public AuditoriaService(IEventoAuditoriaRepository eventos)
    {
        _eventos = eventos;
    }

    public Task RegistrarAsync(
        ContextoAuditoria contexto,
        string accion,
        string entidad,
        string? entidadId = null,
        string? datos = null,
        CancellationToken ct = default)
    {
        AuditoriaValidator.Validar(contexto, datos);

        var evento = new EventoAuditoria
        {
            Accion = accion,
            Entidad = entidad,
            EntidadId = entidadId,
            ActorTipo = contexto.ActorTipo,
            ContribuyenteId = contexto.ContribuyenteId,
            UsuarioId = contexto.UsuarioId,
            AdministradorId = contexto.AdministradorId,
            Datos = datos,
            CreatedAt = DateTime.UtcNow
        };

        return _eventos.AddAsync(evento, ct);
    }
}
