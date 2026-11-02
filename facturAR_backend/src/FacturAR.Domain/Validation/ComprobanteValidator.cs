using FacturAR.Domain.Entities;
using FacturAR.Domain.Enums;
using FacturAR.Domain.Exceptions;

namespace FacturAR.Domain.Validation;

public sealed record ReceptorValidado(
    string DocumentoTipo,
    string DocumentoNumero,
    string RazonSocial,
    CondicionIva CondicionIva);

public static class ComprobanteValidator
{
    private const int ItemsMinimos = 1;
    private const int ItemsMaximos = 500;
    private const int DescripcionMaxima = 500;
    private const int ObservacionesMaximas = 500;
    private const string DocumentoConsumidorFinal = "99";
    private const string DocumentoTipoCuit = "CUIT";
    private const string MensajeCondicionInadmisible =
        "La condición frente al IVA del receptor no es admisible para este tipo de comprobante.";

    public static void ValidarTipoHabilitado(TipoComprobante tipo)
    {
        if (tipo is not (TipoComprobante.FacturaA or TipoComprobante.FacturaB or TipoComprobante.FacturaC))
        {
            throw new ComprobanteValidationException("El tipo de comprobante no está habilitado en este endpoint.");
        }
    }

    public static void ValidarPuntoVenta(int puntoVenta)
    {
        if (puntoVenta <= 0)
        {
            throw new ComprobanteValidationException("El punto de venta es obligatorio.");
        }
    }

    public static void ValidarEmisor(Contribuyente emisor, TipoComprobante tipo)
    {
        if (emisor.CondicionIva == CondicionIva.ConsumidorFinal && tipo == TipoComprobante.FacturaA)
        {
            throw new EmisionInvalidaException("Un emisor consumidor final no puede emitir una factura A.");
        }
    }

    public static ReceptorValidado ValidarReceptor(
        TipoComprobante tipo,
        string? documentoTipo,
        string? documentoNumero,
        string? razonSocial,
        int? condicionIva)
    {
        var documentoTipoNormalizado = (documentoTipo ?? string.Empty).Trim();
        var documentoNumeroNormalizado = (documentoNumero ?? string.Empty).Trim();
        var razonSocialNormalizada = (razonSocial ?? string.Empty).Trim();

        return tipo switch
        {
            TipoComprobante.FacturaA => ValidarReceptorFacturaA(
                documentoTipoNormalizado,
                documentoNumeroNormalizado,
                razonSocialNormalizada,
                condicionIva),
            TipoComprobante.FacturaB => ValidarReceptorFacturaB(
                documentoTipoNormalizado,
                documentoNumeroNormalizado,
                razonSocialNormalizada,
                condicionIva),
            TipoComprobante.FacturaC => ValidarReceptorFacturaC(
                documentoTipoNormalizado,
                documentoNumeroNormalizado,
                razonSocialNormalizada,
                condicionIva),
            _ => throw new ComprobanteValidationException(
                "El tipo de comprobante no está habilitado en este endpoint.")
        };
    }

    public static void ValidarItems(IReadOnlyList<ComprobanteItem> items, IReadOnlyList<decimal> alicuotasPermitidas)
    {
        if (items.Count is < ItemsMinimos or > ItemsMaximos)
        {
            throw new ComprobanteValidationException("El comprobante debe tener entre 1 y 500 ítems.");
        }

        foreach (var item in items)
        {
            var descripcion = (item.Descripcion ?? string.Empty).Trim();
            if (descripcion.Length == 0 || descripcion.Length > DescripcionMaxima)
            {
                throw new ComprobanteValidationException("Cada ítem debe tener descripción de 1 a 500 caracteres.");
            }

            if (item.Cantidad <= 0)
            {
                throw new ComprobanteValidationException("La cantidad de cada ítem debe ser mayor que cero.");
            }

            if (item.PrecioUnitario < 0)
            {
                throw new ComprobanteValidationException("El precio unitario no puede ser negativo.");
            }

            if (!alicuotasPermitidas.Contains(item.AlicuotaIva))
            {
                throw new ComprobanteValidationException("La alícuota de IVA no está permitida.");
            }
        }
    }

    public static void ValidarObservaciones(string? observaciones)
    {
        if ((observaciones ?? string.Empty).Trim().Length > ObservacionesMaximas)
        {
            throw new ComprobanteValidationException("Las observaciones admiten hasta 500 caracteres.");
        }
    }

    public static void ValidarImporteTotal(decimal? informado, decimal calculado)
    {
        if (informado is { } valor && Redondear2(valor) != calculado)
        {
            throw new ComprobanteValidationException("El importe total no coincide con el cálculo de los ítems.");
        }
    }

    private static ReceptorValidado ValidarReceptorFacturaA(
        string documentoTipo,
        string documentoNumero,
        string razonSocial,
        int? condicionIva)
    {
        if (!CuitValidator.IsValid(documentoNumero))
        {
            throw new CuitInvalidoException("El receptor de una factura A debe tener un CUIT válido.");
        }

        if (condicionIva != (int)CondicionIva.ResponsableInscripto)
        {
            throw new ComprobanteValidationException(MensajeCondicionInadmisible);
        }

        if (razonSocial.Length == 0)
        {
            throw new ComprobanteValidationException("La razón social del receptor es obligatoria.");
        }

        return new ReceptorValidado(
            documentoTipo,
            CuitValidator.Normalize(documentoNumero),
            razonSocial,
            CondicionIva.ResponsableInscripto);
    }

    private static ReceptorValidado ValidarReceptorFacturaB(
        string documentoTipo,
        string documentoNumero,
        string razonSocial,
        int? condicionIva)
    {
        if (razonSocial.Length == 0)
        {
            throw new ComprobanteValidationException("La razón social del receptor es obligatoria.");
        }

        var documentoNumeroNormalizado = documentoNumero;
        if (documentoTipo == DocumentoConsumidorFinal)
        {
            documentoNumeroNormalizado = CuitValidator.Normalize(documentoNumero);
            if (!EsDocumentoConsumidorFinalValido(documentoNumeroNormalizado))
            {
                throw new CuitInvalidoException("El documento del receptor no es válido.");
            }
        }
        else
        {
            if (!CuitValidator.IsValid(documentoNumero))
            {
                throw new CuitInvalidoException("El documento del receptor no es válido.");
            }

            documentoNumeroNormalizado = CuitValidator.Normalize(documentoNumero);
        }

        if (condicionIva is null)
        {
            throw new ComprobanteValidationException(MensajeCondicionInadmisible);
        }

        var condicion = (CondicionIva)condicionIva.Value;
        if (condicion is not (CondicionIva.ConsumidorFinal or CondicionIva.Monotributo or CondicionIva.Exento))
        {
            throw new ComprobanteValidationException(MensajeCondicionInadmisible);
        }

        return new ReceptorValidado(documentoTipo, documentoNumeroNormalizado, razonSocial, condicion);
    }

    private static ReceptorValidado ValidarReceptorFacturaC(
        string documentoTipo,
        string documentoNumero,
        string razonSocial,
        int? condicionIva)
    {
        var documentoNumeroNormalizado = documentoNumero;
        if (string.Equals(documentoTipo, DocumentoTipoCuit, StringComparison.OrdinalIgnoreCase))
        {
            if (!CuitValidator.IsValid(documentoNumero))
            {
                throw new CuitInvalidoException("El documento del receptor no es válido.");
            }

            documentoNumeroNormalizado = CuitValidator.Normalize(documentoNumero);
        }
        else if (documentoTipo != DocumentoConsumidorFinal)
        {
            throw new ComprobanteValidationException("La factura C se emite a consumidor final.");
        }

        if (condicionIva != (int)CondicionIva.ConsumidorFinal)
        {
            throw new ComprobanteValidationException(MensajeCondicionInadmisible);
        }

        return new ReceptorValidado(
            documentoTipo,
            documentoNumeroNormalizado,
            razonSocial,
            CondicionIva.ConsumidorFinal);
    }

    private static bool EsDocumentoConsumidorFinalValido(string documentoNormalizado)
        => documentoNormalizado.Length is 7 or 8
            || (documentoNormalizado.Length == 11 && CuitValidator.IsValid(documentoNormalizado));

    private static decimal Redondear2(decimal valor) => Math.Round(valor, 2, MidpointRounding.AwayFromZero);
}
