using System.Globalization;
using FacturAR.Application.Comprobantes;
using FacturAR.Domain.Entities;
using FacturAR.Domain.Enums;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FacturAR.Infrastructure.Pdf;

public sealed class ComprobantePdfModelo(Comprobante comprobante, byte[] qrPng) : IDocument
{
    public const string LeyendaSimulacion = "Comprobante simulado sin validez fiscal";

    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-AR");

    public DocumentMetadata GetMetadata() => new()
    {
        Title = $"Comprobante {comprobante.PuntoVenta:D4}-{comprobante.Numero:D8}",
        Author = comprobante.EmisorRazonSocial,
        Subject = comprobante.TipoComprobante.ObtenerDescripcion(),
        Creator = "FacturAR",
        Producer = "FacturAR",
        CreationDate = comprobante.FechaEmision,
        ModifiedDate = comprobante.FechaEmision
    };

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(2, Unit.Centimetre);
            page.DefaultTextStyle(estilo => estilo.FontSize(9));

            page.Header().Element(ComponerEncabezado);
            page.Content().Element(ComponerCuerpo);
            page.Footer().Element(ComponerPie);
        });
    }

    private void ComponerEncabezado(IContainer container)
    {
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(emisor =>
                {
                    emisor.Item().Text(comprobante.EmisorRazonSocial).Bold().FontSize(13);
                    emisor.Item().Text($"CUIT: {comprobante.EmisorCuit}");
                    emisor.Item().Text($"Punto de venta: {comprobante.PuntoVenta:D4}");
                });

                row.ConstantItem(110).Column(qr =>
                {
                    qr.Item().Image(qrPng);
                    qr.Item().AlignCenter().Text("Verificación").FontSize(7);
                });
            });

            column.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Grey.Medium);

            column.Item().PaddingTop(8).Row(row =>
            {
                row.RelativeItem().Text(comprobante.TipoComprobante.ObtenerDescripcion()).Bold().FontSize(12);
                row.RelativeItem().AlignRight().Text($"N° {comprobante.PuntoVenta:D4}-{comprobante.Numero:D8}");
                row.RelativeItem().AlignRight().Text($"Fecha de emisión: {FormatearFecha(comprobante.FechaEmision)}");
            });
        });
    }

    private void ComponerCuerpo(IContainer container)
    {
        container.PaddingVertical(10).Column(column =>
        {
            column.Spacing(12);

            column.Item().Border(1).BorderColor(Colors.Grey.Medium).Padding(8).Column(receptor =>
            {
                receptor.Item().Text("Receptor").Bold();
                receptor.Item().Text($"Razón social: {comprobante.ReceptorRazonSocial}");
                receptor.Item().Text($"Documento: {comprobante.ReceptorDocumentoTipo} {comprobante.ReceptorDocumentoNumero}");
                receptor.Item().Text($"Condición frente al IVA: {DescripcionCondicionIva(comprobante.ReceptorCondicionIva)}");
            });

            column.Item().Table(ComponerItems);

            column.Item().AlignRight().Width(220).Column(totales =>
            {
                totales.Item().Row(row =>
                {
                    row.RelativeItem().Text("Importe neto");
                    row.ConstantItem(90).AlignRight().Text(Monto(comprobante.ImporteNeto));
                });

                totales.Item().Row(row =>
                {
                    row.RelativeItem().Text("IVA");
                    row.ConstantItem(90).AlignRight().Text(Monto(comprobante.ImporteIva));
                });

                totales.Item().BorderTop(1).BorderColor(Colors.Grey.Medium).PaddingTop(4).Row(row =>
                {
                    row.RelativeItem().Text("Total").Bold();
                    row.ConstantItem(90).AlignRight().Text(Monto(comprobante.ImporteTotal)).Bold();
                });
            });

            column.Item().Column(cae =>
            {
                cae.Item().Text($"CAE: {comprobante.Cae}").Bold();
                cae.Item().Text($"Vencimiento del CAE: {FormatearFecha(comprobante.CaeVencimiento)}");
            });
        });
    }

    private void ComponerItems(TableDescriptor table)
    {
        table.ColumnsDefinition(columns =>
        {
            columns.RelativeColumn(4);
            columns.RelativeColumn(1);
            columns.RelativeColumn(2);
            columns.RelativeColumn(1);
            columns.RelativeColumn(2);
        });

        table.Header(header =>
        {
            header.Cell().Element(EstiloEncabezado).Text("Descripción").Bold();
            header.Cell().Element(EstiloEncabezado).AlignRight().Text("Cantidad").Bold();
            header.Cell().Element(EstiloEncabezado).AlignRight().Text("Precio unitario").Bold();
            header.Cell().Element(EstiloEncabezado).AlignRight().Text("IVA %").Bold();
            header.Cell().Element(EstiloEncabezado).AlignRight().Text("Subtotal").Bold();
        });

        foreach (var item in comprobante.Items)
        {
            table.Cell().Element(EstiloCelda).Text(item.Descripcion);
            table.Cell().Element(EstiloCelda).AlignRight().Text(item.Cantidad.ToString("N2", Cultura));
            table.Cell().Element(EstiloCelda).AlignRight().Text(Monto(item.PrecioUnitario));
            table.Cell().Element(EstiloCelda).AlignRight().Text(item.AlicuotaIva.ToString("N2", Cultura));
            table.Cell().Element(EstiloCelda).AlignRight().Text(Monto(item.Subtotal));
        }
    }

    private static void ComponerPie(IContainer container)
    {
        container.Column(column =>
        {
            column.Item().LineHorizontal(1).LineColor(Colors.Grey.Medium);
            column.Item().PaddingTop(4).AlignCenter().Text(LeyendaSimulacion).Bold();
            column.Item().AlignCenter().Text(texto =>
            {
                texto.DefaultTextStyle(estilo => estilo.FontSize(7));
                texto.Span("Página ");
                texto.CurrentPageNumber();
                texto.Span(" de ");
                texto.TotalPages();
            });
        });
    }

    private static IContainer EstiloEncabezado(IContainer container)
        => container
            .Background(Colors.Grey.Lighten3)
            .BorderBottom(1)
            .BorderColor(Colors.Grey.Medium)
            .Padding(4);

    private static IContainer EstiloCelda(IContainer container)
        => container
            .BorderBottom(1)
            .BorderColor(Colors.Grey.Lighten2)
            .Padding(4);

    private static string Monto(decimal importe) => importe.ToString("N2", Cultura);

    private static string FormatearFecha(DateTime fecha) => fecha.ToString("dd/MM/yyyy", Cultura);

    private static string DescripcionCondicionIva(CondicionIva condicion) => condicion switch
    {
        CondicionIva.ResponsableInscripto => "Responsable Inscripto",
        CondicionIva.Exento => "Exento",
        CondicionIva.ConsumidorFinal => "Consumidor Final",
        CondicionIva.Monotributo => "Monotributo",
        CondicionIva.SujetoNoCategorizado => "Sujeto No Categorizado",
        _ => "Sin categorizar"
    };
}
