using FacturAR.Application.Comprobantes;
using FacturAR.Domain.Entities;
using FacturAR.Domain.Enums;
using FacturAR.Infrastructure.Pdf;
using QuestPDF.Infrastructure;

namespace FacturAR.UnitTests;

public sealed class ComprobantePdfModeloTests
{
    [Theory]
    [InlineData(1, 1, "0001-00000001.pdf")]
    [InlineData(12, 345, "0012-00000345.pdf")]
    public void NombreArchivo_UsaPuntoDeVentaDeCuatroDigitosYNumeroDeOcho(
        int puntoVenta,
        long numero,
        string esperado)
    {
        var comprobante = new Comprobante { PuntoVenta = puntoVenta, Numero = numero };

        Assert.Equal(esperado, ComprobantePdfNombres.NombreArchivo(comprobante));
        Assert.Matches("^[0-9]{4}-[0-9]{8}\\.pdf$", ComprobantePdfNombres.NombreArchivo(comprobante));
    }

    [Theory]
    [InlineData("http://localhost:5116", "http://localhost:5116/verificar?cae=12345678901234")]
    [InlineData("http://localhost:5116/", "http://localhost:5116/verificar?cae=12345678901234")]
    [InlineData("https://portal.facturar.test", "https://portal.facturar.test/verificar?cae=12345678901234")]
    public void UrlVerificacion_ApuntaALaPaginaPublicaConElCae(string baseUrl, string esperado)
    {
        Assert.Equal(esperado, ComprobantePdfNombres.UrlVerificacion(baseUrl, "12345678901234"));
    }

    [Fact]
    public void UrlVerificacion_EscapaElCae()
    {
        var url = ComprobantePdfNombres.UrlVerificacion("http://localhost:5116/", "12 34&56");

        Assert.Equal("http://localhost:5116/verificar?cae=12%2034%2656", url);
    }

    [Fact]
    public async Task GenerarAsync_ProduceUnPdfValido()
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var comprobante = new Comprobante
        {
            EmisorCuit = "20123456786",
            EmisorRazonSocial = "Empresa de Prueba S.A.",
            PuntoVenta = 1,
            Numero = 1,
            TipoComprobante = TipoComprobante.FacturaC,
            FechaEmision = new DateTime(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc),
            Cae = "12345678901234",
            CaeVencimiento = new DateTime(2026, 11, 4, 0, 0, 0, DateTimeKind.Utc),
            ReceptorDocumentoTipo = "99",
            ReceptorDocumentoNumero = "0",
            ReceptorRazonSocial = "Consumidor Final",
            ReceptorCondicionIva = CondicionIva.ConsumidorFinal,
            ImporteNeto = 1000m,
            ImporteIva = 0m,
            ImporteTotal = 1000m,
            Items =
            [
                new ComprobanteItem
                {
                    Descripcion = "Servicio de prueba",
                    Cantidad = 1m,
                    PrecioUnitario = 1000m,
                    AlicuotaIva = 0m,
                    Subtotal = 1000m
                }
            ]
        };

        var servicio = new QuestPdfComprobanteService(new QrGenerador(), "http://localhost:5116");
        var bytes = await servicio.GenerarAsync(comprobante);

        Assert.True(bytes.Length > 1000);
        Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(bytes, 0, 5));
    }
}
