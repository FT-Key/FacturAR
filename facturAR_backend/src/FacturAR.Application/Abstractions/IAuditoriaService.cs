namespace FacturAR.Application.Abstractions;

public interface IAuditoriaService
{
    Task RegistrarAsync(
        ContextoAuditoria contexto,
        string accion,
        string entidad,
        string? entidadId = null,
        string? datos = null,
        CancellationToken ct = default);
}
