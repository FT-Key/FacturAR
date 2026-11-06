using FacturAR.Application.ApiKeys;
using FacturAR.Domain.Exceptions;

namespace FacturAR.UnitTests;

public sealed class RevocarApiKeyServiceTests
{
    [Fact]
    public async Task Revocar_KeyDelContribuyente_LaMarcaInactivaYRegistraFecha()
    {
        var ctx = new ApiKeysContexto();
        var key = ctx.AgregarKey(prefix: "9f3a1c7b2e40");
        var antes = DateTime.UtcNow;

        await ctx.Revocar.RevocarAsync(key.Id, ctx.Contribuyente.Id, ctx.UsuarioId);

        Assert.False(key.Activa);
        Assert.NotNull(key.RevocadaAt);
        Assert.Equal(DateTimeKind.Utc, key.RevocadaAt!.Value.Kind);
        Assert.InRange(key.RevocadaAt.Value, antes, DateTime.UtcNow);
        Assert.Equal(1, ctx.ApiKeys.Actualizaciones);
    }

    [Fact]
    public async Task Revocar_KeyDeOtroContribuyente_LanzaNoEncontradaYNoLaModifica()
    {
        var ctx = new ApiKeysContexto();
        var key = ctx.AgregarKey(contribuyenteId: Guid.NewGuid());

        await Assert.ThrowsAsync<ApiKeyNoEncontradaException>(
            () => ctx.Revocar.RevocarAsync(key.Id, ctx.Contribuyente.Id, ctx.UsuarioId));

        Assert.True(key.Activa);
        Assert.Null(key.RevocadaAt);
        Assert.Equal(0, ctx.ApiKeys.Actualizaciones);
        Assert.Empty(ctx.Eventos.Eventos);
    }

    [Fact]
    public async Task Revocar_KeyInexistente_LanzaNoEncontrada()
    {
        var ctx = new ApiKeysContexto();

        await Assert.ThrowsAsync<ApiKeyNoEncontradaException>(
            () => ctx.Revocar.RevocarAsync(Guid.NewGuid(), ctx.Contribuyente.Id, ctx.UsuarioId));

        Assert.Empty(ctx.Eventos.Eventos);
    }

    [Fact]
    public async Task Revocar_Exitosa_RegistraAuditoriaSinHashNiValor()
    {
        var ctx = new ApiKeysContexto();
        var key = ctx.AgregarKey(prefix: "9f3a1c7b2e40");
        var valor = $"fak_{key.Prefix}_{new string('A', 43)}";
        key.Hash = ctx.Hasher.Hash(valor);

        await ctx.Revocar.RevocarAsync(key.Id, ctx.Contribuyente.Id, ctx.UsuarioId);

        var evento = Assert.Single(ctx.Eventos.Eventos);
        ctx.AssertEventoSinSecretos(evento, RevocarApiKeyService.AccionRevocada, valor);
        Assert.Equal(ctx.UsuarioId, evento.UsuarioId);
        Assert.Equal(ctx.Contribuyente.Id, evento.ContribuyenteId);
        Assert.Equal(key.Id.ToString(), evento.EntidadId);
        Assert.Equal("Empresa", evento.ActorTipo);
    }
}
