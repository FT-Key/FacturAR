using System.Text.Json;
using FacturAR.Application.ApiKeys;
using FacturAR.Contracts.ApiKeys;

namespace FacturAR.UnitTests;

public sealed class ListarApiKeysServiceTests
{
    [Fact]
    public async Task Listar_DevuelveSoloLasKeysDelContribuyenteDeLaSesion()
    {
        var ctx = new ApiKeysContexto();
        ctx.AgregarKey(nombre: "Ecommerce", prefix: "000000000001");
        ctx.AgregarKey(nombre: "Facturación", prefix: "000000000002");
        ctx.AgregarKey(contribuyenteId: Guid.NewGuid(), nombre: "Ajena", prefix: "000000000003");

        var respuesta = await ctx.Listar.ListarAsync(ctx.Contribuyente.Id);

        Assert.Equal(2, respuesta.Items.Count);
        Assert.DoesNotContain(respuesta.Items, i => i.Nombre == "Ajena");
    }

    [Fact]
    public async Task Listar_NuncaExponeHashValorNiSecreto()
    {
        var ctx = new ApiKeysContexto();
        var prefix = "9f3a1c7b2e40";
        var valor = $"fak_{prefix}_{new string('Z', 43)}";
        var key = ctx.AgregarKey(nombre: "Ecommerce", prefix: prefix, hash: ctx.Hasher.Hash(valor));

        var respuesta = await ctx.Listar.ListarAsync(ctx.Contribuyente.Id);
        var serializado = JsonSerializer.Serialize(respuesta);

        Assert.DoesNotContain(key.Hash, serializado, StringComparison.Ordinal);
        Assert.DoesNotContain(valor, serializado, StringComparison.Ordinal);
        Assert.DoesNotContain(new string('Z', 43), serializado, StringComparison.Ordinal);
        Assert.DoesNotContain("hash", serializado, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("valor", serializado, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", serializado, StringComparison.OrdinalIgnoreCase);

        var propiedades = typeof(ApiKeyResumenResponse).GetProperties().Select(p => p.Name).ToList();
        Assert.DoesNotContain("Hash", propiedades);
        Assert.DoesNotContain("Valor", propiedades);
        Assert.DoesNotContain("Secret", propiedades);
    }

    [Fact]
    public async Task Listar_MuestraNombrePrefijoEstadoFechaDeCreacionYUltimoUso()
    {
        var ctx = new ApiKeysContexto();
        var key = ctx.AgregarKey(nombre: "Ecommerce", prefix: "9f3a1c7b2e40");
        key.UltimoUso = new DateTime(2026, 10, 28, 15, 30, 0, DateTimeKind.Utc);

        var respuesta = await ctx.Listar.ListarAsync(ctx.Contribuyente.Id);

        var item = Assert.Single(respuesta.Items);
        Assert.Equal(key.Id, item.Id);
        Assert.Equal("Ecommerce", item.Nombre);
        Assert.Equal("fak_9f3a1c7b2e40", item.Prefix);
        Assert.True(item.Activa);
        Assert.Equal(key.CreatedAt, item.CreatedAt);
        Assert.Equal(key.UltimoUso, item.UltimoUso);
        Assert.Null(item.RevocadaAt);
        Assert.Null(item.RegeneradaAt);
    }

    [Fact]
    public async Task Listar_SinKeys_DevuelveListaVacia()
    {
        var ctx = new ApiKeysContexto();

        var respuesta = await ctx.Listar.ListarAsync(ctx.Contribuyente.Id);

        Assert.Empty(respuesta.Items);
    }
}
