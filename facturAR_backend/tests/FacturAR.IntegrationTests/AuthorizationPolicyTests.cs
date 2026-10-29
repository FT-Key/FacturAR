using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace FacturAR.IntegrationTests;

public sealed class AuthorizationPolicyTests
{
    [Fact]
    public async Task LasPoliticasDistinguenLosRolesDeCadaPlano()
    {
        await using var factory = new CustomWebApplicationFactory();
        using var scope = factory.Services.CreateScope();
        var autorizacion = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();

        var administrador = Principal(
            ("contribuyenteId", Guid.NewGuid().ToString()),
            ("rol", "Administrador"),
            ("plano", "Empresa"));

        var emisor = Principal(
            ("contribuyenteId", Guid.NewGuid().ToString()),
            ("rol", "Emisor"),
            ("plano", "Empresa"));

        var consulta = Principal(
            ("contribuyenteId", Guid.NewGuid().ToString()),
            ("rol", "Consulta"),
            ("plano", "Empresa"));

        var soporte = Principal(("rolGlobal", "Soporte"), ("plano", "Administrador"));
        var superAdmin = Principal(("rolGlobal", "SuperAdmin"), ("plano", "Administrador"));

        Assert.True(await Puede(autorizacion, administrador, "EmpresaAdministrador"));
        Assert.True(await Puede(autorizacion, administrador, "EmpresaEmisor"));
        Assert.True(await Puede(autorizacion, administrador, "EmpresaConsulta"));

        Assert.False(await Puede(autorizacion, emisor, "EmpresaAdministrador"));
        Assert.True(await Puede(autorizacion, emisor, "EmpresaEmisor"));
        Assert.True(await Puede(autorizacion, emisor, "EmpresaConsulta"));

        Assert.False(await Puede(autorizacion, consulta, "EmpresaAdministrador"));
        Assert.False(await Puede(autorizacion, consulta, "EmpresaEmisor"));
        Assert.True(await Puede(autorizacion, consulta, "EmpresaConsulta"));

        Assert.True(await Puede(autorizacion, soporte, "AdminGlobal"));
        Assert.False(await Puede(autorizacion, soporte, "AdminSuper"));
        Assert.True(await Puede(autorizacion, superAdmin, "AdminGlobal"));
        Assert.True(await Puede(autorizacion, superAdmin, "AdminSuper"));

        Assert.False(await Puede(autorizacion, administrador, "AdminGlobal"));
        Assert.False(await Puede(autorizacion, soporte, "EmpresaAutenticado"));
    }

    private static async Task<bool> Puede(
        IAuthorizationService autorizacion,
        ClaimsPrincipal principal,
        string politica)
    {
        var resultado = await autorizacion.AuthorizeAsync(principal, politica);
        return resultado.Succeeded;
    }

    private static ClaimsPrincipal Principal(params (string Tipo, string Valor)[] claims)
        => new(new ClaimsIdentity(
            claims.Select(c => new Claim(c.Tipo, c.Valor)),
            authenticationType: "test"));
}