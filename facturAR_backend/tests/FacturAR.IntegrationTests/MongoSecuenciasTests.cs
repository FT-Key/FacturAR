using FacturAR.Application.Comprobantes;
using FacturAR.Domain.Entities;
using FacturAR.Domain.Enums;
using FacturAR.Domain.Exceptions;
using FacturAR.Infrastructure.Mongo;
using FacturAR.Infrastructure.Repositories;
using MongoDB.Driver;

namespace FacturAR.IntegrationTests;

public sealed class MongoSecuenciasTests : IAsyncLifetime
{
    private static readonly Guid ContribuyenteA = Guid.NewGuid();
    private static readonly Guid ContribuyenteB = Guid.NewGuid();

    private readonly string _databaseName = $"facturar_it_{Guid.NewGuid():N}"[..28];

    private IMongoClient? _mongoClient;
    private MongoDbContext? _context;

    public async Task InitializeAsync()
    {
        if (string.IsNullOrWhiteSpace(MongoTestSettings.ConnectionString))
        {
            return;
        }

        MongoSerialization.Configure();
        _mongoClient = new MongoClient(MongoTestSettings.GetConnectionStringWithTimeout());
        _context = new MongoDbContext(_mongoClient.GetDatabase(_databaseName));

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await MongoIndexInitializer.InitializeAsync(_context, timeout.Token);
    }

    public Task DisposeAsync()
    {
        _mongoClient?.DropDatabase(_databaseName);
        return Task.CompletedTask;
    }

    [MongoFact]
    public async Task CP0701_PrimerNumero_EsUnoYLaSecuenciaSeCreaDeFormaImplicita()
    {
        var numero = await ObtenerSiguienteAsync(ContribuyenteA, 1, TipoComprobante.FacturaC);

        Assert.Equal(1, numero);

        var secuencia = await SecuenciaAsync(ContribuyenteA, 1, TipoComprobante.FacturaC);
        Assert.Equal(1, secuencia.UltimoNumero);
        Assert.NotEqual(Guid.Empty, secuencia.Id);
    }

    [MongoFact]
    public async Task CP0702_SecuenciaEnCuarentaYUno_DevuelveCuarentaYDos()
    {
        await SembrarSecuenciaAsync(ContribuyenteA, 1, TipoComprobante.FacturaC, 41);

        var numero = await ObtenerSiguienteAsync(ContribuyenteA, 1, TipoComprobante.FacturaC);

        Assert.Equal(42, numero);
        Assert.Equal(42, (await SecuenciaAsync(ContribuyenteA, 1, TipoComprobante.FacturaC)).UltimoNumero);
    }

    [MongoFact]
    public async Task CP0707_LaSecuenciaNoSeComparteEntreContribuyentes()
    {
        await SembrarSecuenciaAsync(ContribuyenteA, 1, TipoComprobante.FacturaC, 7);

        var numero = await ObtenerSiguienteAsync(ContribuyenteB, 1, TipoComprobante.FacturaC);

        Assert.Equal(1, numero);
        Assert.Equal(7, (await SecuenciaAsync(ContribuyenteA, 1, TipoComprobante.FacturaC)).UltimoNumero);
    }

    [MongoFact]
    public async Task CP0708_LaSecuenciaNoSeComparteEntrePuntosDeVenta()
    {
        Assert.Equal(1, await ObtenerSiguienteAsync(ContribuyenteA, 1, TipoComprobante.FacturaC));
        Assert.Equal(2, await ObtenerSiguienteAsync(ContribuyenteA, 1, TipoComprobante.FacturaC));

        Assert.Equal(1, await ObtenerSiguienteAsync(ContribuyenteA, 2, TipoComprobante.FacturaC));
    }

    [MongoFact]
    public async Task CP0709_LaSecuenciaNoSeComparteEntreTiposDeComprobante()
    {
        Assert.Equal(1, await ObtenerSiguienteAsync(ContribuyenteA, 1, TipoComprobante.FacturaC));
        Assert.Equal(2, await ObtenerSiguienteAsync(ContribuyenteA, 1, TipoComprobante.FacturaC));

        Assert.Equal(1, await ObtenerSiguienteAsync(ContribuyenteA, 1, TipoComprobante.FacturaB));
    }

    [MongoFact]
    public async Task CP0711_PuntoVentaInactivo_NoConsumeNumero()
    {
        var servicio = CrearServicio();
        var puntosVenta = new PuntoVentaRepository(_context!, new MongoSesionActual());

        await puntosVenta.InsertarAsync(new PuntoVenta
        {
            ContribuyenteId = ContribuyenteA,
            Numero = 2,
            Descripcion = "Inactivo",
            Activo = false
        });
        await SembrarSecuenciaAsync(ContribuyenteA, 2, TipoComprobante.FacturaC, 9);

        await Assert.ThrowsAsync<PuntoVentaInactivoException>(
            () => servicio.ObtenerSiguienteNumeroAsync(ContribuyenteA, 2, TipoComprobante.FacturaC));

        Assert.Equal(9, (await SecuenciaAsync(ContribuyenteA, 2, TipoComprobante.FacturaC)).UltimoNumero);
    }

    [MongoFact]
    public async Task PuntoVentaDeOtroContribuyente_DevuelveNoEncontrado()
    {
        var servicio = CrearServicio();
        var puntosVenta = new PuntoVentaRepository(_context!, new MongoSesionActual());

        await puntosVenta.InsertarAsync(new PuntoVenta
        {
            ContribuyenteId = ContribuyenteB,
            Numero = 1,
            Descripcion = "Ajeno",
            Activo = true
        });

        await Assert.ThrowsAsync<PuntoVentaNoEncontradoException>(
            () => servicio.ObtenerSiguienteNumeroAsync(ContribuyenteA, 1, TipoComprobante.FacturaC));

        Assert.Equal(0, await _context!.Secuencias.CountDocumentsAsync(FilterDefinition<Secuencia>.Empty));
    }

    [MongoFact]
    public async Task CP0703_DosEmisionesConcurrentes_ObtienenNumerosDistintosYConsecutivos()
    {
        await SembrarSecuenciaAsync(ContribuyenteA, 1, TipoComprobante.FacturaC, 20);

        var numeros = await Task.WhenAll(
            EmitirAsync(ContribuyenteA, 1, TipoComprobante.FacturaC),
            EmitirAsync(ContribuyenteA, 1, TipoComprobante.FacturaC));

        long[] esperados = [21L, 22L];
        Assert.Equal(esperados, numeros.Order());
        await AssertNumeracionPersistidaAsync(ContribuyenteA, 1, TipoComprobante.FacturaC, esperados);
    }

    [MongoFact]
    public async Task CP0704_SeisEmisionesConcurrentes_ObtienenNumerosCorrelativosSinRepeticiones()
    {
        (Guid ContribuyenteId, int PuntoVenta, TipoComprobante Tipo, long Inicial)[] escenarios =
        [
            (ContribuyenteA, 1, TipoComprobante.FacturaC, 10),
            (ContribuyenteB, 2, TipoComprobante.FacturaB, 100),
            (ContribuyenteA, 3, TipoComprobante.FacturaA, 0)
        ];

        foreach (var escenario in escenarios)
        {
            await SembrarSecuenciaAsync(escenario.ContribuyenteId, escenario.PuntoVenta, escenario.Tipo, escenario.Inicial);

            var numeros = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ =>
                EmitirAsync(escenario.ContribuyenteId, escenario.PuntoVenta, escenario.Tipo)));

            var esperados = Enumerable.Range(1, 6).Select(i => escenario.Inicial + i).ToArray();
            Assert.Equal(esperados, numeros.Order());
            await AssertNumeracionPersistidaAsync(
                escenario.ContribuyenteId,
                escenario.PuntoVenta,
                escenario.Tipo,
                esperados);
            Assert.Equal(
                esperados[^1],
                (await SecuenciaAsync(escenario.ContribuyenteId, escenario.PuntoVenta, escenario.Tipo)).UltimoNumero);
        }
    }

    [MongoFact]
    public async Task CP0703_CuatroEmisionesConcurrentesSobreSecuenciaInexistente_DevuelvenUnoACuatro()
    {
        var numeros = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ =>
            EmitirAsync(ContribuyenteA, 9, TipoComprobante.NotaCreditoC)));

        long[] esperados = [1L, 2L, 3L, 4L];
        Assert.Equal(esperados, numeros.Order());
        await AssertNumeracionPersistidaAsync(ContribuyenteA, 9, TipoComprobante.NotaCreditoC, esperados);
        Assert.Equal(4, (await SecuenciaAsync(ContribuyenteA, 9, TipoComprobante.NotaCreditoC)).UltimoNumero);
    }

    [MongoFact]
    public async Task CP0712_TransaccionQueFalla_NoConsumeElNumero()
    {
        const string mensaje = "Falla forzada de la prueba de rollback.";
        await SembrarSecuenciaAsync(ContribuyenteA, 1, TipoComprobante.FacturaC, 5);

        var sesionActual = new MongoSesionActual();
        var transaccion = new TransaccionMongo(_mongoClient!, sesionActual);
        var repositorio = new SecuenciaRepository(_context!, sesionActual);

        var excepcion = await Assert.ThrowsAsync<InvalidOperationException>(() => transaccion.EjecutarAsync<long>(async ct =>
        {
            var numero = await repositorio.ObtenerSiguienteNumeroAsync(ContribuyenteA, 1, TipoComprobante.FacturaC, ct);
            await sesionActual.InsertOneAsync(_context!.Comprobantes, NuevoComprobante(ContribuyenteA, 1, TipoComprobante.FacturaC, numero), ct);
            throw new InvalidOperationException(mensaje);
        }));

        Assert.Equal(mensaje, excepcion.Message);
        Assert.Equal(5, (await SecuenciaAsync(ContribuyenteA, 1, TipoComprobante.FacturaC)).UltimoNumero);
        Assert.Equal(0, await _context!.Comprobantes.CountDocumentsAsync(FilterDefinition<Comprobante>.Empty));

        var siguiente = await EmitirAsync(ContribuyenteA, 1, TipoComprobante.FacturaC);

        Assert.Equal(6, siguiente);
        Assert.Equal(6, (await SecuenciaAsync(ContribuyenteA, 1, TipoComprobante.FacturaC)).UltimoNumero);
    }

    private SecuenciaService CrearServicio()
    {
        var sesionActual = new MongoSesionActual();
        return new SecuenciaService(
            new PuntoVentaRepository(_context!, sesionActual),
            new SecuenciaRepository(_context!, sesionActual));
    }

    private async Task<long> ObtenerSiguienteAsync(Guid contribuyenteId, int puntoVenta, TipoComprobante tipo)
    {
        var sesionActual = new MongoSesionActual();
        var transaccion = new TransaccionMongo(_mongoClient!, sesionActual);
        var repositorio = new SecuenciaRepository(_context!, sesionActual);

        return await transaccion.EjecutarAsync(
            ct => repositorio.ObtenerSiguienteNumeroAsync(contribuyenteId, puntoVenta, tipo, ct));
    }

    private async Task<long> EmitirAsync(Guid contribuyenteId, int puntoVenta, TipoComprobante tipo)
    {
        var sesionActual = new MongoSesionActual();
        var transaccion = new TransaccionMongo(_mongoClient!, sesionActual);
        var repositorio = new SecuenciaRepository(_context!, sesionActual);

        return await transaccion.EjecutarAsync(async ct =>
        {
            var numero = await repositorio.ObtenerSiguienteNumeroAsync(contribuyenteId, puntoVenta, tipo, ct);
            await sesionActual.InsertOneAsync(
                _context!.Comprobantes,
                NuevoComprobante(contribuyenteId, puntoVenta, tipo, numero),
                ct);
            return numero;
        });
    }

    private static Comprobante NuevoComprobante(Guid contribuyenteId, int puntoVenta, TipoComprobante tipo, long numero)
        => new()
        {
            ContribuyenteId = contribuyenteId,
            EmisorCuit = "20123456786",
            EmisorRazonSocial = "Demo SRL",
            PuntoVenta = puntoVenta,
            TipoComprobante = tipo,
            Numero = numero,
            Cae = Guid.NewGuid().ToString("N")[..14],
            CaeVencimiento = DateTime.UtcNow.AddDays(10),
            ImporteNeto = 100m,
            ImporteIva = 21m,
            ImporteTotal = 121m
        };

    private Task SembrarSecuenciaAsync(Guid contribuyenteId, int puntoVenta, TipoComprobante tipo, long ultimoNumero)
        => _context!.Secuencias.InsertOneAsync(new Secuencia
        {
            ContribuyenteId = contribuyenteId,
            PuntoVenta = puntoVenta,
            TipoComprobante = tipo,
            UltimoNumero = ultimoNumero
        });

    private Task<Secuencia> SecuenciaAsync(Guid contribuyenteId, int puntoVenta, TipoComprobante tipo)
    {
        var filtro = Builders<Secuencia>.Filter.Eq(s => s.ContribuyenteId, contribuyenteId)
            & Builders<Secuencia>.Filter.Eq(s => s.PuntoVenta, puntoVenta)
            & Builders<Secuencia>.Filter.Eq(s => s.TipoComprobante, tipo);

        return _context!.Secuencias.Find(filtro).SingleAsync();
    }

    private async Task AssertNumeracionPersistidaAsync(
        Guid contribuyenteId,
        int puntoVenta,
        TipoComprobante tipo,
        long[] esperados)
    {
        var filtro = Builders<Comprobante>.Filter.Eq(c => c.ContribuyenteId, contribuyenteId)
            & Builders<Comprobante>.Filter.Eq(c => c.PuntoVenta, puntoVenta)
            & Builders<Comprobante>.Filter.Eq(c => c.TipoComprobante, tipo);

        var numeros = (await _context!.Comprobantes.Find(filtro).ToListAsync()).Select(c => c.Numero).Order().ToArray();

        Assert.Equal(esperados, numeros);
    }
}
