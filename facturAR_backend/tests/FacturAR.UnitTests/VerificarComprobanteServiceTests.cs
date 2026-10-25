using FacturAR.Application.Abstractions;
using FacturAR.Application.Comprobantes;
using FacturAR.Domain.Entities;
using FacturAR.Domain.Enums;

namespace FacturAR.UnitTests;

public sealed class VerificarComprobanteServiceTests
{
    [Fact]
    public async Task VerificarAsync_ConCaeExistente_DevuelveDatosDelComprobante()
    {
        var comprobante = new Comprobante
        {
            Cae = "12345678901234",
            EmisorCuit = "20123456786",
            EmisorRazonSocial = "Empresa de Prueba S.A.",
            TipoComprobante = TipoComprobante.FacturaA,
            PuntoVenta = 1,
            Numero = 42,
            FechaEmision = new DateTime(2026, 10, 25, 0, 0, 0, DateTimeKind.Utc),
            CaeVencimiento = new DateTime(2026, 11, 4, 0, 0, 0, DateTimeKind.Utc),
            ImporteTotal = 121000m,
            Estado = EstadoComprobante.Emitido
        };
        var service = new VerificarComprobanteService(new FakeComprobanteRepository(comprobante));

        var resultado = await service.VerificarAsync(comprobante.Cae);

        Assert.NotNull(resultado);
        Assert.Equal(comprobante.Cae, resultado.Cae);
        Assert.Equal("Factura A", resultado.TipoComprobanteDescripcion);
        Assert.Equal(121000m, resultado.ImporteTotal);
        Assert.Equal(nameof(EstadoComprobante.Emitido), resultado.Estado);
    }

    [Fact]
    public async Task VerificarAsync_ConCaeInexistente_DevuelveNull()
    {
        var service = new VerificarComprobanteService(new FakeComprobanteRepository(null));

        var resultado = await service.VerificarAsync("12345678901234");

        Assert.Null(resultado);
    }

    [Theory]
    [InlineData("123")]
    [InlineData("1234567890123A")]
    [InlineData("")]
    public async Task VerificarAsync_ConCaeInvalido_DevuelveNull(string cae)
    {
        var service = new VerificarComprobanteService(new FakeComprobanteRepository(null));

        var resultado = await service.VerificarAsync(cae);

        Assert.Null(resultado);
    }

    private sealed class FakeComprobanteRepository(Comprobante? comprobante) : IComprobanteRepository
    {
        public Task<Comprobante?> GetByCaeAsync(string cae, CancellationToken cancellationToken = default)
            => Task.FromResult(comprobante);

        public Task AddAsync(Comprobante nuevo, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
