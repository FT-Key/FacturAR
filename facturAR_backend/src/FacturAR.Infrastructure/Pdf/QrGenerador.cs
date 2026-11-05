using QRCoder;

namespace FacturAR.Infrastructure.Pdf;

public sealed class QrGenerador
{
    public byte[] GenerarPng(string contenido, int pixelesPorModulo)
    {
        using var generador = new QRCodeGenerator();
        using var datos = generador.CreateQrCode(contenido, QRCodeGenerator.ECCLevel.Q);
        using var qr = new PngByteQRCode(datos);

        return qr.GetGraphic(pixelesPorModulo);
    }
}
