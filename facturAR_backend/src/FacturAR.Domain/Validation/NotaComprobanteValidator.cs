using FacturAR.Domain.Entities;
using FacturAR.Domain.Enums;
using FacturAR.Domain.Exceptions;

namespace FacturAR.Domain.Validation;

public static class NotaComprobanteValidator
{
    public const string MensajeOriginalAusente = "Falta el comprobante original de la nota.";
    public const string MensajeOriginalEnFactura = "Una factura no admite comprobante original.";
    public const string MensajeOriginalNoEncontrado =
        "El comprobante original no existe o no pertenece al contribuyente autenticado.";
    public const string MensajeTipoIncompatible = "El tipo de nota no es compatible con el comprobante original.";
    public const string MensajeNotaSobreNota = "Una nota no puede referenciar otra nota.";
    public const string MensajeOriginalAnulado = "El comprobante original está anulado y no admite notas.";
    public const string MensajeOriginalNoAutorizado = "El comprobante original no está autorizado y no admite notas.";
    public const string MensajeImporteExcedido =
        "El importe de la nota de crédito supera el saldo ajustable del comprobante original.";
    public const string MensajeSinSaldo = "El comprobante original no tiene saldo ajustable.";

    public static bool EsNota(TipoComprobante tipo) => EsNotaCredito(tipo) || EsNotaDebito(tipo);

    public static bool EsNotaCredito(TipoComprobante tipo)
        => tipo is TipoComprobante.NotaCreditoA or TipoComprobante.NotaCreditoB or TipoComprobante.NotaCreditoC;

    public static bool EsNotaDebito(TipoComprobante tipo)
        => tipo is TipoComprobante.NotaDebitoA or TipoComprobante.NotaDebitoB or TipoComprobante.NotaDebitoC;

    public static TipoComprobante TipoBase(TipoComprobante tipo) => tipo switch
    {
        TipoComprobante.FacturaA or TipoComprobante.NotaDebitoA or TipoComprobante.NotaCreditoA => TipoComprobante.FacturaA,
        TipoComprobante.FacturaB or TipoComprobante.NotaDebitoB or TipoComprobante.NotaCreditoB => TipoComprobante.FacturaB,
        TipoComprobante.FacturaC or TipoComprobante.NotaDebitoC or TipoComprobante.NotaCreditoC => TipoComprobante.FacturaC,
        _ => throw new ComprobanteValidationException("El tipo de comprobante no está habilitado en este endpoint.")
    };

    public static void ValidarReferencia(TipoComprobante tipo, Guid? comprobanteOriginalId)
    {
        var tieneOriginal = comprobanteOriginalId is Guid id && id != Guid.Empty;

        if (EsNota(tipo) && !tieneOriginal)
        {
            throw new ComprobanteValidationException(MensajeOriginalAusente);
        }

        if (!EsNota(tipo) && comprobanteOriginalId is not null)
        {
            throw new ComprobanteValidationException(MensajeOriginalEnFactura);
        }
    }

    public static void ValidarOriginal(TipoComprobante tipoNota, Comprobante original)
    {
        if (EsNota(original.TipoComprobante) || original.ComprobanteOriginalId is not null)
        {
            throw new TipoNotaIncompatibleException(MensajeNotaSobreNota);
        }

        if (TipoBase(tipoNota) != original.TipoComprobante)
        {
            throw new TipoNotaIncompatibleException(MensajeTipoIncompatible);
        }

        switch (original.Estado)
        {
            case EstadoComprobante.Emitido:
                return;
            case EstadoComprobante.Anulado or EstadoComprobante.Rechazado:
                throw new ComprobanteAnuladoException(MensajeOriginalAnulado);
            default:
                throw new ComprobanteAnuladoException(MensajeOriginalNoAutorizado);
        }
    }

    public static decimal SaldoActual(Comprobante original)
        => original.ImporteOriginalAjustado ?? original.ImporteTotal;

    public static decimal CalcularNuevoSaldo(TipoComprobante tipoNota, decimal saldo, decimal importeNota)
    {
        if (EsNotaDebito(tipoNota))
        {
            return saldo + importeNota;
        }

        if (saldo <= 0m)
        {
            throw new ImporteExcedidoException(MensajeSinSaldo);
        }

        if (importeNota > saldo)
        {
            throw new ImporteExcedidoException(MensajeImporteExcedido);
        }

        return saldo - importeNota;
    }
}
