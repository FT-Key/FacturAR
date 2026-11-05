using FacturAR.Application.Abstractions;
using FacturAR.Application.Comprobantes;
using FacturAR.Domain.Entities;
using QuestPDF.Fluent;

namespace FacturAR.Infrastructure.Pdf;

public sealed class QuestPdfComprobanteService(QrGenerador qrGenerador, string portalBaseUrl) : IPdfComprobanteService
{
    private const int PixelesPorModuloQr = 8;

    public Task<byte[]> GenerarAsync(Comprobante comprobante, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var qrPng = qrGenerador.GenerarPng(
            ComprobantePdfNombres.UrlVerificacion(portalBaseUrl, comprobante.Cae),
            PixelesPorModuloQr);

        var pdf = new ComprobantePdfModelo(comprobante, qrPng).GeneratePdf();

        return Task.FromResult(pdf);
    }
}
