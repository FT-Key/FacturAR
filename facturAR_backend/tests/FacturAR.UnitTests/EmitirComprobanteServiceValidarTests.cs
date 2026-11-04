using FacturAR.Domain.Exceptions;

namespace FacturAR.UnitTests;

public sealed class EmitirComprobanteServiceValidarTests
{
    [Fact]
    public async Task ValidarAsync_ComprobanteValido_NoPersisteNiConsumeNumero()
    {
        var contexto = new ComprobanteContexto();

        await contexto.Servicio.ValidarAsync(
            contexto.Emisor.Id,
            "Emisor",
            contexto.FacturaC());

        Assert.Equal(0, contexto.Secuencias.Llamadas);
        Assert.Equal(0, contexto.Transaccion.Ejecuciones);
        Assert.Empty(contexto.Comprobantes.Comprobantes);
        Assert.Empty(contexto.Eventos.Eventos);
    }

    [Fact]
    public async Task ValidarAsync_ImporteIncorrecto_LanzaValidacionSinEscribir()
    {
        var contexto = new ComprobanteContexto();

        await Assert.ThrowsAsync<ComprobanteValidationException>(
            () => contexto.Servicio.ValidarAsync(
                contexto.Emisor.Id,
                "Emisor",
                contexto.FacturaC(importeTotal: 999m)));

        Assert.Equal(0, contexto.Secuencias.Llamadas);
        Assert.Equal(0, contexto.Transaccion.Ejecuciones);
        Assert.Empty(contexto.Comprobantes.Comprobantes);
        Assert.Empty(contexto.Eventos.Eventos);
    }

    [Fact]
    public async Task ValidarAsync_PuntoVentaDeOtraEmpresa_RechazaSinEscribir()
    {
        var contexto = new ComprobanteContexto();

        await Assert.ThrowsAsync<ComprobanteNoEncontradoException>(
            () => contexto.Servicio.ValidarAsync(
                contexto.Emisor.Id,
                "Emisor",
                contexto.FacturaC() with { PuntoVenta = 2 }));

        Assert.Equal(0, contexto.Secuencias.Llamadas);
        Assert.Equal(0, contexto.Transaccion.Ejecuciones);
        Assert.Empty(contexto.Comprobantes.Comprobantes);
        Assert.Empty(contexto.Eventos.Eventos);
    }
}
