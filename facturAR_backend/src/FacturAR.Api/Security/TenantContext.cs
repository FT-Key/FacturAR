using System.Security.Claims;
using FacturAR.Application.Abstractions;

namespace FacturAR.Api.Security;

public sealed class TenantContext : ITenantContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TenantContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? ContribuyenteId => LeerGuid("contribuyenteId");

    public Guid? UsuarioId
        => Plano == "Empresa" ? LeerGuid(ClaimTypes.NameIdentifier) : null;

    public Guid? AdministradorId
        => Plano == "Administrador" ? LeerGuid(ClaimTypes.NameIdentifier) : null;

    private string? Plano
    {
        get
        {
            var user = _httpContextAccessor.HttpContext?.User;
            return user?.Identity?.IsAuthenticated == true
                ? user.FindFirst("plano")?.Value
                : null;
        }
    }

    private Guid? LeerGuid(string claimType)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var valor = user.FindFirst(claimType)?.Value;
        return Guid.TryParse(valor, out var id) ? id : null;
    }
}