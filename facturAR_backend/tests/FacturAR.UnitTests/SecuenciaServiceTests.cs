using FacturAR.Application.Abstractions;
using FacturAR.Application.Comprobantes;
using FacturAR.Domain.Entities;
using FacturAR.Domain.Enums;
using FacturAR.Domain.Exceptions;

namespace FacturAR.UnitTests;

public sealed class SecuenciaServiceTests
{
    private static readonly Guid ContribuyenteId = Guid.NewGuid();

    [Fact]
    public async Task ObtenerSiguienteNumero_PuntoVentaInexistente_LanzaNoEncontradoSinTocarLaSecuencia()
    {
        var puntosVenta = new PuntoVentaRepositoryFake(null);
        var secuencias = new SecuenciaRepositoryFake(1);
        var servicio = new SecuenciaService(puntosVenta, secuencias);

        await Assert.ThrowsAsync<PuntoVentaNoEncontradoException>(
            () => servicio.ObtenerSiguienteNumeroAsync(ContribuyenteId, 1, TipoComprobante.FacturaC));

        Assert.Equal(0, secuencias.Llamadas);
        Assert.Equal((ContribuyenteId, 1), puntosVenta.UltimaConsulta);
    }

    [Fact]
    public async Task ObtenerSiguienteNumero_PuntoVentaInactivo_LanzaInactivoSinTocarLaSecuencia()
    {
        var puntosVenta = new PuntoVentaRepositoryFake(new PuntoVenta
        {
            ContribuyenteId = ContribuyenteId,
            Numero = 2,
            Activo = false
        });
        var secuencias = new SecuenciaRepositoryFake(1);
        var servicio = new SecuenciaService(puntosVenta, secuencias);

        await Assert.ThrowsAsync<PuntoVentaInactivoException>(
            () => servicio.ObtenerSiguienteNumeroAsync(ContribuyenteId, 2, TipoComprobante.FacturaC));

        Assert.Equal(0, secuencias.Llamadas);
    }

    [Fact]
    public async Task ObtenerSiguienteNumero_PuntoVentaActivo_DevuelveElNumeroDelRepositorioConLaClaveExacta()
    {
        var puntosVenta = new PuntoVentaRepositoryFake(new PuntoVenta
        {
            ContribuyenteId = ContribuyenteId,
            Numero = 1,
            Activo = true
        });
        var secuencias = new SecuenciaRepositoryFake(42);
        var servicio = new SecuenciaService(puntosVenta, secuencias);

        var numero = await servicio.ObtenerSiguienteNumeroAsync(ContribuyenteId, 1, TipoComprobante.FacturaB);

        Assert.Equal(42, numero);
        Assert.Equal(1, secuencias.Llamadas);
        Assert.Equal((ContribuyenteId, 1, TipoComprobante.FacturaB), secuencias.UltimaClave);
    }

    private sealed class PuntoVentaRepositoryFake(PuntoVenta? respuesta) : IPuntoVentaRepository
    {
        public (Guid ContribuyenteId, int Numero)? UltimaConsulta { get; private set; }

        public Task InsertarAsync(PuntoVenta puntoVenta, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<PuntoVenta?> ObtenerPorNumeroAsync(Guid contribuyenteId, int numero, CancellationToken ct = default)
        {
            UltimaConsulta = (contribuyenteId, numero);
            return Task.FromResult(respuesta);
        }
    }

    private sealed class SecuenciaRepositoryFake(long numero) : ISecuenciaRepository
    {
        public int Llamadas { get; private set; }

        public (Guid ContribuyenteId, int PuntoVenta, TipoComprobante Tipo)? UltimaClave { get; private set; }

        public Task<long> ObtenerSiguienteNumeroAsync(
            Guid contribuyenteId,
            int puntoVenta,
            TipoComprobante tipo,
            CancellationToken ct = default)
        {
            Llamadas++;
            UltimaClave = (contribuyenteId, puntoVenta, tipo);
            return Task.FromResult(numero);
        }
    }
}
