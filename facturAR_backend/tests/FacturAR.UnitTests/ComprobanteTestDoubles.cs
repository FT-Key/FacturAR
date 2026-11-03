using FacturAR.Application.Abstractions;
using FacturAR.Application.Comprobantes;
using FacturAR.Contracts.Comprobantes;
using FacturAR.Domain.Entities;
using FacturAR.Domain.Enums;

namespace FacturAR.UnitTests;

internal sealed class ComprobanteContexto
{
    public const string CuitEmisor = "20123456786";
    public const string CuitReceptor = "30712345671";

    public ComprobanteContexto()
    {
        Contribuyentes.Agregar(Emisor);
        PuntosVenta.Agregar(new PuntoVenta
        {
            ContribuyenteId = Emisor.Id,
            Numero = 1,
            Descripcion = "Principal",
            Activo = true
        });
        PuntosVenta.Agregar(new PuntoVenta
        {
            ContribuyenteId = Guid.NewGuid(),
            Numero = 2,
            Descripcion = "Ajeno",
            Activo = true
        });

        Servicio = new EmitirComprobanteService(
            Contribuyentes,
            PuntosVenta,
            Secuencias,
            Comprobantes,
            Eventos,
            Transaccion,
            Parametros);
    }

    public Contribuyente Emisor { get; } = new()
    {
        RazonSocial = "Demo SRL",
        Cuit = CuitEmisor,
        CondicionIva = CondicionIva.ResponsableInscripto,
        Activo = true
    };

    public ComprobanteContribuyentesFake Contribuyentes { get; } = new();

    public ComprobantePuntosVentaFake PuntosVenta { get; } = new();

    public ComprobanteSecuenciasFake Secuencias { get; } = new();

    public ComprobanteRepositorioFake Comprobantes { get; } = new();

    public ComprobanteEventosFake Eventos { get; } = new();

    public ComprobanteTransaccionFake Transaccion { get; } = new();

    public ComprobanteParametrosFake Parametros { get; } = new();

    public EmitirComprobanteService Servicio { get; }

    public EmitirComprobanteRequest FacturaA(
        decimal? importeTotal = 12100m,
        decimal cantidad = 2m,
        decimal precioUnitario = 5000m,
        decimal alicuotaIva = 21m)
        => new(
            (int)TipoComprobante.FacturaA,
            1,
            null,
            new ReceptorComprobanteRequest(
                "CUIT",
                CuitReceptor,
                "Cliente S.A.",
                (int)CondicionIva.ResponsableInscripto),
            [new ItemComprobanteRequest("Servicio", cantidad, precioUnitario, alicuotaIva)],
            importeTotal,
            null);

    public EmitirComprobanteRequest FacturaB(decimal? importeTotal = 12100m)
        => new(
            (int)TipoComprobante.FacturaB,
            1,
            null,
            new ReceptorComprobanteRequest(
                "99",
                "30123456",
                "Cliente S.A.",
                (int)CondicionIva.ConsumidorFinal),
            [new ItemComprobanteRequest("Producto", 1m, 12100m, 21m)],
            importeTotal,
            null);

    public EmitirComprobanteRequest FacturaC(decimal alicuotaIva = 0m, decimal? importeTotal = 10000m)
        => new(
            (int)TipoComprobante.FacturaC,
            1,
            null,
            new ReceptorComprobanteRequest(
                "99",
                string.Empty,
                string.Empty,
                (int)CondicionIva.ConsumidorFinal),
            [new ItemComprobanteRequest("Producto", 1m, 10000m, alicuotaIva)],
            importeTotal,
            null);
}

internal sealed class ComprobanteContribuyentesFake : IContribuyenteRepository
{
    public List<Contribuyente> Contribuyentes { get; } = [];

    public int Consultas { get; private set; }

    public void Agregar(Contribuyente contribuyente) => Contribuyentes.Add(contribuyente);

    public Task<Contribuyente?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        Consultas++;
        return Task.FromResult(Contribuyentes.FirstOrDefault(c => c.Id == id));
    }

    public Task<bool> ExistePorCuitAsync(string cuit, CancellationToken ct = default)
        => Task.FromResult(Contribuyentes.Any(c => c.Cuit == cuit));

    public Task InsertarAsync(Contribuyente contribuyente, CancellationToken ct = default)
    {
        Contribuyentes.Add(contribuyente);
        return Task.CompletedTask;
    }
}

internal sealed class ComprobantePuntosVentaFake : IPuntoVentaRepository
{
    public List<PuntoVenta> PuntosVenta { get; } = [];

    public int Consultas { get; private set; }

    public void Agregar(PuntoVenta puntoVenta) => PuntosVenta.Add(puntoVenta);

    public Task InsertarAsync(PuntoVenta puntoVenta, CancellationToken ct = default)
    {
        PuntosVenta.Add(puntoVenta);
        return Task.CompletedTask;
    }

    public Task<PuntoVenta?> ObtenerPorNumeroAsync(
        Guid contribuyenteId,
        int numero,
        CancellationToken ct = default)
    {
        Consultas++;
        return Task.FromResult(PuntosVenta.FirstOrDefault(
            p => p.ContribuyenteId == contribuyenteId && p.Numero == numero));
    }
}

internal sealed class ComprobanteSecuenciasFake : ISecuenciaService
{
    public long Numero { get; set; } = 42;

    public int Llamadas { get; private set; }

    public (Guid ContribuyenteId, int PuntoVenta, TipoComprobante Tipo)? UltimaClave { get; private set; }

    public Task<long> ObtenerSiguienteNumeroAsync(
        Guid contribuyenteId,
        int puntoVenta,
        TipoComprobante tipo,
        CancellationToken ct = default)
    {
        Llamadas++;
        UltimaClave = (contribuyenteId, puntoVenta, tipo);
        return Task.FromResult(Numero);
    }
}

internal sealed class ComprobanteRepositorioFake : IComprobanteRepository
{
    public List<Comprobante> Comprobantes { get; } = [];

    public Task<Comprobante?> GetByCaeAsync(string cae, CancellationToken cancellationToken = default)
        => Task.FromResult(Comprobantes.FirstOrDefault(c => c.Cae == cae));

    public Task<Comprobante?> GetByIdAsync(
        Guid contribuyenteId,
        Guid id,
        CancellationToken cancellationToken = default)
        => Task.FromResult(Comprobantes.FirstOrDefault(
            c => c.Id == id && c.ContribuyenteId == contribuyenteId));

    public Task AddAsync(Comprobante comprobante, CancellationToken cancellationToken = default)
    {
        Comprobantes.Add(comprobante);
        return Task.CompletedTask;
    }

    public int ConflictosSimulados { get; set; }

    public int AjustesRechazados { get; private set; }

    public Task<bool> AjustarSaldoAsync(
        Guid contribuyenteId,
        Guid originalId,
        decimal? saldoEsperado,
        decimal saldoNuevo,
        CancellationToken cancellationToken = default)
    {
        var original = Comprobantes.FirstOrDefault(c => c.Id == originalId
            && c.ContribuyenteId == contribuyenteId
            && c.ComprobanteOriginalId is null
            && c.ImporteOriginalAjustado == saldoEsperado);

        if (original is null || ConflictosSimulados > 0)
        {
            ConflictosSimulados = Math.Max(0, ConflictosSimulados - 1);
            AjustesRechazados++;
            return Task.FromResult(false);
        }

        original.ImporteOriginalAjustado = saldoNuevo;
        return Task.FromResult(true);
    }
}

internal sealed class ComprobanteEventosFake : IEventoAuditoriaRepository
{
    public List<EventoAuditoria> Eventos { get; } = [];

    public Task AddAsync(EventoAuditoria evento, CancellationToken ct = default)
    {
        Eventos.Add(evento);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<EventoAuditoria>> ListarPorEntidadAsync(
        string entidad,
        string entidadId,
        CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<EventoAuditoria>>(
            Eventos.Where(e => e.Entidad == entidad && e.EntidadId == entidadId).ToList());
}

internal sealed class ComprobanteTransaccionFake : ITransaccionMongo
{
    public int Ejecuciones { get; private set; }

    public Task<T> EjecutarAsync<T>(
        Func<CancellationToken, Task<T>> operacion,
        CancellationToken ct = default)
    {
        Ejecuciones++;
        return operacion(ct);
    }
}

internal sealed class ComprobanteParametrosFake : IParametrosResolver
{
    public int DiasVencimiento { get; set; } = 10;

    public IReadOnlyList<decimal> Alicuotas { get; set; } = [0m, 5m, 10.5m, 21m, 27m];

    public string Moneda { get; set; } = "ARS";

    public Task<string?> ObtenerAsync(
        string clave,
        Guid? contribuyenteId,
        CancellationToken ct = default)
        => Task.FromResult<string?>(null);

    public Task<int> ObtenerDiasVencimientoCaeAsync(Guid? contribuyenteId, CancellationToken ct = default)
        => Task.FromResult(DiasVencimiento);

    public Task<IReadOnlyList<decimal>> ObtenerAlicuotasPermitidasAsync(
        Guid? contribuyenteId,
        CancellationToken ct = default)
        => Task.FromResult(Alicuotas);

    public Task<string> ObtenerMonedaAsync(Guid? contribuyenteId, CancellationToken ct = default)
        => Task.FromResult(Moneda);
}
