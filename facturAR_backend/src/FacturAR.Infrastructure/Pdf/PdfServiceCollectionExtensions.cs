using FacturAR.Application.Abstractions;
using FacturAR.Application.Comprobantes;
using Microsoft.Extensions.DependencyInjection;
using QuestPDF.Infrastructure;

namespace FacturAR.Infrastructure.Pdf;

public static class PdfServiceCollectionExtensions
{
    public static IServiceCollection AddComprobantePdf(this IServiceCollection services, string portalBaseUrl)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var baseNormalizada = ComprobantePdfNombres.NormalizarBaseUrl(portalBaseUrl);

        services.AddSingleton<QrGenerador>();
        services.AddSingleton<IPdfComprobanteService>(sp => new QuestPdfComprobanteService(
            sp.GetRequiredService<QrGenerador>(),
            baseNormalizada));

        return services;
    }
}
