using System.Diagnostics;
using System.Text;
using FacturAR.Domain.Entities;
using FacturAR.Domain.Enums;
using FacturAR.Infrastructure.Pdf;
using QuestPDF.Infrastructure;
using StbImageSharp;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using ZXing;
using ZXing.Common;

namespace FacturAR.UnitTests;

public sealed class ComprobantePdfContenidoTests
{
    private const string PortalBaseUrl = "http://localhost:5116";
    private const string Cae = "12345678901234";
    private const string RutaMongo = "mongodb://127.0.0.1:27017/facturar";
    private const string IdempotencyKey = "idem-interna-no-visible-123";

    private static readonly Guid ContribuyenteId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly DateTime FechaEmision = new(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime VencimientoCae = new(2026, 11, 4, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GenerarAsync_IncluyeLosDatosFiscalesDelComprobante()
    {
        var texto = await GenerarTextoAsync();

        Assert.Contains("Empresa de Prueba S.A.", texto, StringComparison.Ordinal);
        Assert.Contains("CUIT: 20123456786", texto, StringComparison.Ordinal);
        Assert.Contains("Punto de venta: 0001", texto, StringComparison.Ordinal);

        Assert.Contains("Factura A", texto, StringComparison.Ordinal);
        Assert.Contains("N° 0001-00000001", texto, StringComparison.Ordinal);
        Assert.Contains("Fecha de emisión: 25/10/2026", texto, StringComparison.Ordinal);

        Assert.Contains("Receptor", texto, StringComparison.Ordinal);
        Assert.Contains("Razón social: Cliente S.A.", texto, StringComparison.Ordinal);
        Assert.Contains("Documento: CUIT 20345678901", texto, StringComparison.Ordinal);
        Assert.Contains("Condición frente al IVA: Responsable Inscripto", texto, StringComparison.Ordinal);

        Assert.Contains("Servicio de consultoría", texto, StringComparison.Ordinal);
        Assert.Contains("Producto de prueba", texto, StringComparison.Ordinal);
        Assert.Contains("Precio unitario", texto, StringComparison.Ordinal);
        Assert.Contains("Subtotal", texto, StringComparison.Ordinal);

        Assert.Contains("Importe neto", texto, StringComparison.Ordinal);
        Assert.Contains("3.000,00", texto, StringComparison.Ordinal);
        Assert.Contains("IVA", texto, StringComparison.Ordinal);
        Assert.Contains("630,00", texto, StringComparison.Ordinal);
        Assert.Contains("Total", texto, StringComparison.Ordinal);
        Assert.Contains("3.630,00", texto, StringComparison.Ordinal);

        Assert.Contains($"CAE: {Cae}", texto, StringComparison.Ordinal);
        Assert.Contains("Vencimiento del CAE: 04/11/2026", texto, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerarAsync_IncluyeLaLeyendaDeSimulacion()
    {
        var texto = await GenerarTextoAsync();

        Assert.Contains(
            "Comprobante simulado sin validez fiscal",
            texto,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerarAsync_NoExponeDatosInternosNiRutasDelServidor()
    {
        var texto = await GenerarTextoAsync();

        Assert.DoesNotContain(ContribuyenteId.ToString(), texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ContribuyenteId", texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(IdempotencyKey, texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("IdempotencyKey", texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Hash", texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PasswordHash", texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("mongodb", texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(RutaMongo, texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("27017", texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("C:\\", texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/home/", texto, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerarAsync_ElQrDelPdfApuntaALaVerificacionPublicaPorCae()
    {
        var bytes = await GenerarAsync();
        var contenido = DecodificarQr(bytes);

        Assert.Equal($"{PortalBaseUrl}/verificar?cae={Cae}", contenido);
    }

    [Fact]
    public async Task GenerarAsync_DosGeneracionesConservanElMismoContenidoFiscal()
    {
        var servicio = CrearServicio();

        var primero = await servicio.GenerarAsync(NuevoComprobante());
        var segundo = await servicio.GenerarAsync(NuevoComprobante());

        Assert.Equal("%PDF-", Encoding.ASCII.GetString(primero, 0, 5));
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(segundo, 0, 5));
        Assert.Equal(TextoDe(primero), TextoDe(segundo));
        Assert.Equal(DecodificarQr(primero), DecodificarQr(segundo));
    }

    [Fact]
    public async Task GenerarAsync_ConWarmUp_CompletaEnMenosDeTresSegundos()
    {
        var servicio = CrearServicio();
        await servicio.GenerarAsync(NuevoComprobante());

        var cronometro = Stopwatch.StartNew();
        var bytes = await servicio.GenerarAsync(NuevoComprobante());
        cronometro.Stop();

        Assert.True(bytes.Length > 0);
        Assert.True(
            cronometro.Elapsed < TimeSpan.FromSeconds(3),
            $"La generación tardó {cronometro.Elapsed.TotalMilliseconds:N0} ms.");
    }

    private static QuestPdfComprobanteService CrearServicio()
    {
        QuestPDF.Settings.License = LicenseType.Community;

        return new QuestPdfComprobanteService(new QrGenerador(), PortalBaseUrl);
    }

    private static Task<byte[]> GenerarAsync()
        => CrearServicio().GenerarAsync(NuevoComprobante());

    private static async Task<string> GenerarTextoAsync()
        => TextoDe(await GenerarAsync());

    private static string TextoDe(byte[] pdf)
    {
        using var documento = PdfDocument.Open(pdf);

        var texto = new StringBuilder();
        foreach (var pagina in documento.GetPages())
        {
            texto.AppendLine(pagina.Text);
        }

        return Normalizar(texto.ToString());
    }

    private static string Normalizar(string texto)
        => string.Join(' ', texto.Split((char[])[' ', '\r', '\n', '\t'],
            StringSplitOptions.RemoveEmptyEntries));

    private static string DecodificarQr(byte[] pdf)
    {
        using var documento = PdfDocument.Open(pdf);
        var imagen = documento.GetPages().SelectMany(pagina => pagina.GetImages()).First();

        Assert.True(imagen.TryGetPng(out var png), "El PDF no contiene una imagen PNG embebida.");

        var bitmap = ImageResult.FromMemory(png!, ColorComponents.RedGreenBlueAlpha);

        var lector = new BarcodeReaderGeneric
        {
            AutoRotate = false,
            Options = new DecodingOptions
            {
                TryHarder = true,
                PossibleFormats = [BarcodeFormat.QR_CODE]
            }
        };

        var resultado = lector.Decode(
            new RGBLuminanceSource(bitmap.Data, bitmap.Width, bitmap.Height, RGBLuminanceSource.BitmapFormat.RGBA32));

        Assert.NotNull(resultado);
        Assert.Equal(BarcodeFormat.QR_CODE, resultado!.BarcodeFormat);

        return resultado.Text;
    }

    private static Comprobante NuevoComprobante()
        => new()
        {
            ContribuyenteId = ContribuyenteId,
            EmisorCuit = "20123456786",
            EmisorRazonSocial = "Empresa de Prueba S.A.",
            PuntoVenta = 1,
            Numero = 1,
            TipoComprobante = TipoComprobante.FacturaA,
            FechaEmision = FechaEmision,
            Cae = Cae,
            CaeVencimiento = VencimientoCae,
            ReceptorDocumentoTipo = "CUIT",
            ReceptorDocumentoNumero = "20345678901",
            ReceptorRazonSocial = "Cliente S.A.",
            ReceptorCondicionIva = CondicionIva.ResponsableInscripto,
            ImporteNeto = 3000m,
            ImporteIva = 630m,
            ImporteTotal = 3630m,
            Moneda = "ARS",
            Estado = EstadoComprobante.Emitido,
            IdempotencyKey = IdempotencyKey,
            Items =
            [
                new ComprobanteItem
                {
                    Descripcion = "Servicio de consultoría",
                    Cantidad = 2m,
                    PrecioUnitario = 1000m,
                    AlicuotaIva = 21m,
                    Subtotal = 2000m
                },
                new ComprobanteItem
                {
                    Descripcion = "Producto de prueba",
                    Cantidad = 1m,
                    PrecioUnitario = 1000m,
                    AlicuotaIva = 21m,
                    Subtotal = 1000m
                }
            ]
        };
}
