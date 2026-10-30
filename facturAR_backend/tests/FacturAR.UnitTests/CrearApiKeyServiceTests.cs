using System.Text.Json;
using FacturAR.Application.ApiKeys;
using FacturAR.Domain.Exceptions;

namespace FacturAR.UnitTests;

public sealed class CrearApiKeyServiceTests
{
    [Fact]
    public async Task Crear_NombreValido_DevuelveElValorCompletoYUnoSoloGuardaPrefijoYHash()
    {
        var ctx = new ApiKeysContexto();

        var respuesta = await ctx.Crear.CrearAsync(ctx.Contribuyente.Id, ctx.UsuarioId, "  Ecommerce  ");

        Assert.Equal("Ecommerce", respuesta.Nombre);
        Assert.Matches("^[0-9a-f]{12}$", ctx.ApiKeys.ApiKeys.Single().Prefix);
        Assert.Equal($"fak_{ctx.ApiKeys.ApiKeys.Single().Prefix}", respuesta.Prefix);
        Assert.Equal(FakeApiKeyGenerator.Valor(1).Valor, respuesta.Valor);
        Assert.Equal(DateTimeKind.Utc, respuesta.CreatedAt.Kind);

        var guardada = ctx.ApiKeys.ApiKeys.Single();
        Assert.Equal(ctx.Contribuyente.Id, guardada.ContribuyenteId);
        Assert.Equal(ctx.Hasher.Hash(respuesta.Valor), guardada.Hash);
        Assert.Matches("^[0-9a-f]{64}$", guardada.Hash);
        Assert.NotEqual(respuesta.Valor, guardada.Hash);
        Assert.True(guardada.Activa);
        Assert.Null(guardada.RegeneradaAt);

        var documento = JsonSerializer.Serialize(guardada);
        Assert.DoesNotContain(respuesta.Valor, documento, StringComparison.Ordinal);
        Assert.DoesNotContain(FakeApiKeyGenerator.Valor(1).Secret, documento, StringComparison.Ordinal);
        Assert.Contains(guardada.Prefix, documento, StringComparison.Ordinal);
        Assert.Contains(guardada.Hash, documento, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("   \t ")]
    public async Task Crear_NombreVacioOEnBlanco_LanzaValidacionSinInsertarNiAuditar(string nombre)
    {
        var ctx = new ApiKeysContexto();

        await Assert.ThrowsAsync<ApiKeyValidationException>(
            () => ctx.Crear.CrearAsync(ctx.Contribuyente.Id, ctx.UsuarioId, nombre));

        Assert.Empty(ctx.ApiKeys.ApiKeys);
        Assert.Equal(0, ctx.ApiKeys.IntentosInsercion);
        Assert.Empty(ctx.Eventos.Eventos);
    }

    [Fact]
    public async Task Crear_NombreDeSesentaYCincoCaracteres_LanzaValidacion()
    {
        var ctx = new ApiKeysContexto();

        await Assert.ThrowsAsync<ApiKeyValidationException>(
            () => ctx.Crear.CrearAsync(ctx.Contribuyente.Id, ctx.UsuarioId, new string('a', 65)));

        Assert.Empty(ctx.ApiKeys.ApiKeys);
    }

    [Fact]
    public async Task Crear_NombreDeSesentaYCuatroCaracteres_EsValido()
    {
        var ctx = new ApiKeysContexto();

        var respuesta = await ctx.Crear.CrearAsync(ctx.Contribuyente.Id, ctx.UsuarioId, new string('a', 64));

        Assert.Equal(64, respuesta.Nombre.Length);
        Assert.Single(ctx.ApiKeys.ApiKeys);
    }

    [Fact]
    public async Task Crear_NombreConCaracterDeControl_LanzaValidacion()
    {
        var ctx = new ApiKeysContexto();

        await Assert.ThrowsAsync<ApiKeyValidationException>(
            () => ctx.Crear.CrearAsync(ctx.Contribuyente.Id, ctx.UsuarioId, "Ecommerce\u0001"));

        Assert.Empty(ctx.ApiKeys.ApiKeys);
        Assert.Empty(ctx.Eventos.Eventos);
    }

    [Fact]
    public async Task Crear_ContribuyenteInactivo_LanzaCuentaInactivaSinInsertar()
    {
        var ctx = new ApiKeysContexto();
        ctx.Contribuyente.Activo = false;

        await Assert.ThrowsAsync<CuentaInactivaException>(
            () => ctx.Crear.CrearAsync(ctx.Contribuyente.Id, ctx.UsuarioId, "Ecommerce"));

        Assert.Empty(ctx.ApiKeys.ApiKeys);
        Assert.Empty(ctx.Eventos.Eventos);
    }

    [Fact]
    public async Task Crear_ContribuyenteInexistente_LanzaCuentaInactiva()
    {
        var ctx = new ApiKeysContexto();

        await Assert.ThrowsAsync<CuentaInactivaException>(
            () => ctx.Crear.CrearAsync(Guid.NewGuid(), ctx.UsuarioId, "Ecommerce"));

        Assert.Empty(ctx.ApiKeys.ApiKeys);
    }

    [Fact]
    public async Task Crear_PrefijoDuplicado_ReintentaHastaObtenerUnoLibre()
    {
        var ctx = new ApiKeysContexto();
        ctx.ApiKeys.PrefijosEnUso.Add(FakeApiKeyGenerator.Valor(1).Prefix);
        ctx.ApiKeys.PrefijosEnUso.Add(FakeApiKeyGenerator.Valor(2).Prefix);

        var respuesta = await ctx.Crear.CrearAsync(ctx.Contribuyente.Id, ctx.UsuarioId, "Ecommerce");

        Assert.Equal(3, ctx.Generator.Llamadas);
        Assert.Equal(3, ctx.ApiKeys.IntentosInsercion);
        Assert.Single(ctx.ApiKeys.ApiKeys);
        Assert.Equal(FakeApiKeyGenerator.Valor(3).Prefix, ctx.ApiKeys.ApiKeys.Single().Prefix);
        Assert.Equal(FakeApiKeyGenerator.Valor(3).Valor, respuesta.Valor);
    }

    [Fact]
    public async Task Crear_PrefijoDuplicadoSiempre_LanzaErrorTrasAgotarLosIntentosConfigurados()
    {
        var ctx = new ApiKeysContexto();
        for (var i = 1; i <= 5; i++)
        {
            ctx.ApiKeys.PrefijosEnUso.Add(FakeApiKeyGenerator.Valor(i).Prefix);
        }

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => ctx.Crear.CrearAsync(ctx.Contribuyente.Id, ctx.UsuarioId, "Ecommerce"));

        Assert.Equal(ctx.Options.MaxIntentosGeneracionPrefijo, ctx.Generator.Llamadas);
        Assert.Equal(ctx.Options.MaxIntentosGeneracionPrefijo, ctx.ApiKeys.IntentosInsercion);
        Assert.Empty(ctx.ApiKeys.ApiKeys);
        Assert.Empty(ctx.Eventos.Eventos);
    }

    [Fact]
    public async Task Crear_Exitosa_RegistraAuditoriaSinHashNiValor()
    {
        var ctx = new ApiKeysContexto();

        var respuesta = await ctx.Crear.CrearAsync(ctx.Contribuyente.Id, ctx.UsuarioId, "Ecommerce");

        var evento = Assert.Single(ctx.Eventos.Eventos);
        ctx.AssertEventoSinSecretos(evento, CrearApiKeyService.AccionCreada, respuesta.Valor);
        Assert.Equal(ctx.UsuarioId, evento.UsuarioId);
        Assert.Equal(ctx.Contribuyente.Id, evento.ContribuyenteId);
        Assert.Equal(respuesta.Id.ToString(), evento.EntidadId);
        Assert.Equal("Empresa", evento.ActorTipo);
        Assert.Contains("Ecommerce", evento.Datos ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains(ctx.ApiKeys.ApiKeys.Single().Prefix, evento.Datos ?? string.Empty, StringComparison.Ordinal);
    }
}
