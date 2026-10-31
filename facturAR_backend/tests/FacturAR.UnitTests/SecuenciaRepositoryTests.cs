using FacturAR.Application.Abstractions;
using FacturAR.Application.Comprobantes;
using FacturAR.Domain.Entities;
using FacturAR.Domain.Enums;
using FacturAR.Infrastructure.Mongo;
using FacturAR.Infrastructure.Repositories;
using MongoDB.Driver;

namespace FacturAR.UnitTests;

public sealed class SecuenciaRepositoryTests
{
    private const string ConnectionStringInalcanzable = "mongodb://127.0.0.1:1/?serverSelectionTimeoutMS=250";

    [Fact]
    public async Task ObtenerSiguienteNumero_SinTransaccionActiva_LanzaInvalidOperationException()
    {
        var repositorio = new SecuenciaRepository(ContextoSinServidor(), new MongoSesionActual());

        var excepcion = await Assert.ThrowsAsync<InvalidOperationException>(
            () => repositorio.ObtenerSiguienteNumeroAsync(Guid.NewGuid(), 1, TipoComprobante.FacturaC));

        Assert.Contains("transacción", excepcion.Message);
    }

    [Fact]
    public async Task ObtenerSiguienteNumero_ServicioConPuntoVentaActivoYSinTransaccion_PropagaLaExcepcionDelGuard()
    {
        var servicio = new SecuenciaService(
            new PuntoVentaActivoRepository(),
            new SecuenciaRepository(ContextoSinServidor(), new MongoSesionActual()));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => servicio.ObtenerSiguienteNumeroAsync(Guid.NewGuid(), 1, TipoComprobante.FacturaC));
    }

    private static MongoDbContext ContextoSinServidor()
        => new(new MongoClient(ConnectionStringInalcanzable).GetDatabase("facturar_unit"));

    private sealed class PuntoVentaActivoRepository : IPuntoVentaRepository
    {
        public Task InsertarAsync(PuntoVenta puntoVenta, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<PuntoVenta?> ObtenerPorNumeroAsync(Guid contribuyenteId, int numero, CancellationToken ct = default)
            => Task.FromResult<PuntoVenta?>(new PuntoVenta
            {
                ContribuyenteId = contribuyenteId,
                Numero = numero,
                Activo = true
            });
    }
}
