using FacturAR.Application.Comprobantes;
using FacturAR.Domain.Entities;

namespace FacturAR.UnitTests;

public sealed class IdempotenciaServiceTests
{
    private static readonly Guid ContribuyenteId = Guid.NewGuid();

    [Fact]
    public async Task ResolverAsync_ClaveLibre_DevuelveNuevo()
    {
        var servicio = new IdempotenciaService(new ComprobanteRepositorioFake());

        var resultado = await servicio.ResolverAsync(ContribuyenteId, "pedido-001", "hash");

        Assert.Equal(EstadoIdempotencia.Nuevo, resultado.Estado);
        Assert.Null(resultado.Comprobante);
    }

    [Fact]
    public async Task ResolverAsync_MismoHash_DevuelveReplayConElComprobante()
    {
        var comprobante = CrearComprobante("pedido-001", "hash");
        var repositorio = new ComprobanteRepositorioFake();
        repositorio.Comprobantes.Add(comprobante);
        var servicio = new IdempotenciaService(repositorio);

        var resultado = await servicio.ResolverAsync(ContribuyenteId, "pedido-001", "hash");

        Assert.Equal(EstadoIdempotencia.Replay, resultado.Estado);
        Assert.Same(comprobante, resultado.Comprobante);
    }

    [Fact]
    public async Task ResolverAsync_HashDistinto_DevuelveConflicto()
    {
        var comprobante = CrearComprobante("pedido-001", "hash-original");
        var repositorio = new ComprobanteRepositorioFake();
        repositorio.Comprobantes.Add(comprobante);
        var servicio = new IdempotenciaService(repositorio);

        var resultado = await servicio.ResolverAsync(ContribuyenteId, "pedido-001", "hash-distinto");

        Assert.Equal(EstadoIdempotencia.Conflicto, resultado.Estado);
        Assert.Same(comprobante, resultado.Comprobante);
    }

    [Fact]
    public async Task ResolverAsync_MismaClaveEnOtroContribuyente_DevuelveNuevo()
    {
        var comprobante = CrearComprobante("pedido-001", "hash");
        var repositorio = new ComprobanteRepositorioFake();
        repositorio.Comprobantes.Add(comprobante);
        var servicio = new IdempotenciaService(repositorio);

        var resultado = await servicio.ResolverAsync(Guid.NewGuid(), "pedido-001", "hash");

        Assert.Equal(EstadoIdempotencia.Nuevo, resultado.Estado);
        Assert.Null(resultado.Comprobante);
    }

    private static Comprobante CrearComprobante(string clave, string hash)
        => new()
        {
            ContribuyenteId = ContribuyenteId,
            IdempotencyKey = clave,
            IdempotencyPayloadHash = hash
        };
}
