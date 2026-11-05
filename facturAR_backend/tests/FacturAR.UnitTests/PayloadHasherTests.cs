using FacturAR.Application.Comprobantes;
using FacturAR.Contracts.Comprobantes;
using FacturAR.Domain.Enums;

namespace FacturAR.UnitTests;

public sealed class PayloadHasherTests
{
    private static readonly DateTime FechaEmision = new(2026, 10, 28, 15, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void Hash_MismoRequest_EsDeterministaYConFormatoHexadecimalMinusculo()
    {
        var request = BaseRequest();

        var primero = PayloadHasher.Hash(request);
        var segundo = PayloadHasher.Hash(request);

        Assert.Equal(primero, segundo);
        Assert.Matches("^[0-9a-f]{64}$", primero);
    }

    [Fact]
    public void Hash_ImporteTotalConDistintaEscala_ProduceElMismoHash()
    {
        var request = BaseRequest();

        var conEntero = PayloadHasher.Hash(request with { ImporteTotal = 100m });
        var conUnDecimal = PayloadHasher.Hash(request with { ImporteTotal = 100.0m });
        var conDosDecimales = PayloadHasher.Hash(request with { ImporteTotal = 100.00m });

        Assert.Equal(conEntero, conUnDecimal);
        Assert.Equal(conEntero, conDosDecimales);
    }

    [Fact]
    public void Hash_CantidadConDistintaEscala_ProduceElMismoHash()
    {
        var request = BaseRequest();

        var conEntero = PayloadHasher.Hash(request with
        {
            Items = [new ItemComprobanteRequest("Servicio", 2m, 5000m, 21m)]
        });
        var conDosDecimales = PayloadHasher.Hash(request with
        {
            Items = [new ItemComprobanteRequest("Servicio", 2.00m, 5000.00m, 21.00m)]
        });

        Assert.Equal(conEntero, conDosDecimales);
    }

    [Fact]
    public void Hash_ItemsEnDistintoOrden_ProduceHashesDistintos()
    {
        var request = BaseRequest();
        var primero = request with
        {
            Items =
            [
                new ItemComprobanteRequest("Servicio A", 1m, 1000m, 21m),
                new ItemComprobanteRequest("Servicio B", 1m, 2000m, 21m)
            ]
        };
        var invertido = primero with
        {
            Items =
            [
                new ItemComprobanteRequest("Servicio B", 1m, 2000m, 21m),
                new ItemComprobanteRequest("Servicio A", 1m, 1000m, 21m)
            ]
        };

        Assert.NotEqual(PayloadHasher.Hash(primero), PayloadHasher.Hash(invertido));
    }

    [Fact]
    public void Hash_DocumentoTipoNuloYVacio_ProducenElMismoHash()
    {
        var request = BaseRequest();

        var nulo = PayloadHasher.Hash(request with
        {
            Receptor = request.Receptor! with { DocumentoTipo = null }
        });
        var vacio = PayloadHasher.Hash(request with
        {
            Receptor = request.Receptor! with { DocumentoTipo = string.Empty }
        });

        Assert.Equal(nulo, vacio);
    }

    [Fact]
    public void Hash_FechaNulaYFechaUtc_ProducenHashesDistintos()
    {
        var request = BaseRequest();

        var nula = PayloadHasher.Hash(request with { FechaEmision = null });
        var conFecha = PayloadHasher.Hash(request with { FechaEmision = FechaEmision });

        Assert.NotEqual(nula, conFecha);
    }

    [Fact]
    public void Hash_FechaConMismoSegundoYDistintosMilisegundos_ProduceElMismoHash()
    {
        var request = BaseRequest();

        var sinMilisegundos = PayloadHasher.Hash(request with
        {
            FechaEmision = new DateTime(2026, 10, 28, 15, 30, 0, DateTimeKind.Utc)
        });
        var conMilisegundos = PayloadHasher.Hash(request with
        {
            FechaEmision = new DateTime(2026, 10, 28, 15, 30, 0, 999, DateTimeKind.Utc)
        });

        Assert.Equal(sinMilisegundos, conMilisegundos);
    }

    [Fact]
    public void Hash_DescripcionYRazonSocialConEspacios_SeNormalizanConTrim()
    {
        var request = BaseRequest();

        var limpio = PayloadHasher.Hash(request);
        var conEspacios = PayloadHasher.Hash(request with
        {
            Receptor = request.Receptor! with { RazonSocial = "  Cliente S.A.  " },
            Items = [new ItemComprobanteRequest("  Servicio  ", 2m, 5000m, 21m)]
        });

        Assert.Equal(limpio, conEspacios);
    }

    private static EmitirComprobanteRequest BaseRequest()
        => new(
            (int)TipoComprobante.FacturaA,
            1,
            FechaEmision,
            new ReceptorComprobanteRequest(
                "CUIT",
                "30712345671",
                "Cliente S.A.",
                (int)CondicionIva.ResponsableInscripto),
            [new ItemComprobanteRequest("Servicio", 2m, 5000m, 21m)],
            12100m,
            "Observación");
}
