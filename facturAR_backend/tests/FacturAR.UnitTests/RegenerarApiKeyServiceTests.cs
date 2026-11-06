using FacturAR.Application.ApiKeys;
using FacturAR.Domain.Exceptions;

namespace FacturAR.UnitTests;

public sealed class RegenerarApiKeyServiceTests
{
    [Fact]
    public async Task Regenerar_KeyDelContribuyente_CambiaHashYDevuelveUnValorNuevoConservandoIdYNombre()
    {
        var ctx = new ApiKeysContexto();
        var key = ctx.AgregarKey(nombre: "Ecommerce", prefix: "9f3a1c7b2e40");
        var hashAnterior = key.Hash;
        var id = key.Id;
        var antes = DateTime.UtcNow;

        var respuesta = await ctx.Regenerar.RegenerarAsync(key.Id, ctx.Contribuyente.Id, ctx.UsuarioId);

        Assert.Equal(id, respuesta.Id);
        Assert.Equal("Ecommerce", respuesta.Nombre);
        Assert.Equal(id, key.Id);
        Assert.Equal("Ecommerce", key.Nombre);
        Assert.NotEqual(hashAnterior, key.Hash);
        Assert.Equal(ctx.Hasher.Hash(respuesta.Valor), key.Hash);
        Assert.Matches("^[0-9a-f]{12}$", key.Prefix);
        Assert.Equal($"fak_{key.Prefix}", respuesta.Prefix);
        Assert.True(key.Activa);
        Assert.Null(key.RevocadaAt);
        Assert.NotNull(key.RegeneradaAt);
        Assert.Equal(DateTimeKind.Utc, key.RegeneradaAt!.Value.Kind);
        Assert.InRange(key.RegeneradaAt.Value, antes, DateTime.UtcNow);
        Assert.Equal(key.RegeneradaAt!.Value, respuesta.RegeneradaAt);
        Assert.Equal(1, ctx.ApiKeys.Actualizaciones);
    }

    [Fact]
    public async Task Regenerar_KeyRevocada_LaReactivaYLimpiaRevocadaAt()
    {
        var ctx = new ApiKeysContexto();
        var key = ctx.AgregarKey(
            prefix: "9f3a1c7b2e40",
            activa: false,
            revocadaAt: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        await ctx.Regenerar.RegenerarAsync(key.Id, ctx.Contribuyente.Id, ctx.UsuarioId);

        Assert.True(key.Activa);
        Assert.Null(key.RevocadaAt);
        Assert.NotNull(key.RegeneradaAt);
    }

    [Fact]
    public async Task Regenerar_KeyDeOtroContribuyente_LanzaNoEncontradaYNoLaModifica()
    {
        var ctx = new ApiKeysContexto();
        var key = ctx.AgregarKey(contribuyenteId: Guid.NewGuid(), prefix: "9f3a1c7b2e40");
        var hashAnterior = key.Hash;

        await Assert.ThrowsAsync<ApiKeyNoEncontradaException>(
            () => ctx.Regenerar.RegenerarAsync(key.Id, ctx.Contribuyente.Id, ctx.UsuarioId));

        Assert.Equal(hashAnterior, key.Hash);
        Assert.True(key.Activa);
        Assert.Null(key.RegeneradaAt);
        Assert.Equal(0, ctx.ApiKeys.Actualizaciones);
        Assert.Empty(ctx.Eventos.Eventos);
    }

    [Fact]
    public async Task Regenerar_KeyInexistente_LanzaNoEncontrada()
    {
        var ctx = new ApiKeysContexto();

        await Assert.ThrowsAsync<ApiKeyNoEncontradaException>(
            () => ctx.Regenerar.RegenerarAsync(Guid.NewGuid(), ctx.Contribuyente.Id, ctx.UsuarioId));

        Assert.Empty(ctx.Eventos.Eventos);
    }

    [Fact]
    public async Task Regenerar_Exitosa_RegistraAuditoriaSinHashNiValor()
    {
        var ctx = new ApiKeysContexto();
        var key = ctx.AgregarKey(prefix: "9f3a1c7b2e40");

        var respuesta = await ctx.Regenerar.RegenerarAsync(key.Id, ctx.Contribuyente.Id, ctx.UsuarioId);

        var evento = Assert.Single(ctx.Eventos.Eventos);
        ctx.AssertEventoSinSecretos(evento, RegenerarApiKeyService.AccionRegenerada, respuesta.Valor);
        Assert.Equal(ctx.UsuarioId, evento.UsuarioId);
        Assert.Equal(ctx.Contribuyente.Id, evento.ContribuyenteId);
        Assert.Equal(key.Id.ToString(), evento.EntidadId);
        Assert.Equal("Empresa", evento.ActorTipo);
    }
}
