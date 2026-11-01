using FacturAR.Application.Parametros;
using FacturAR.Domain.Entities;
using FacturAR.Infrastructure.Mongo;
using FacturAR.Infrastructure.Repositories;
using MongoDB.Driver;

namespace FacturAR.IntegrationTests;

public sealed class MongoParametrosTests : IAsyncLifetime
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
    public async Task CP1803_OverridePorContribuyenteGanaSoloParaEseContribuyente()
    {
        await _context!.Parametros.InsertOneAsync(new Parametro
        {
            ContribuyenteId = ContribuyenteA,
            Clave = "Moneda",
            Valor = "USD"
        });

        var servicio = CrearServicio();

        Assert.Equal("USD", await servicio.ObtenerMonedaAsync(ContribuyenteA));
        Assert.Equal("ARS", await servicio.ObtenerMonedaAsync(ContribuyenteB));
    }

    [MongoFact]
    public async Task ParametrosGlobales_NoSeConsultanEnLaResolucion()
    {
        await _context!.Parametros.InsertOneAsync(new Parametro
        {
            ContribuyenteId = null,
            Clave = "Moneda",
            Valor = "USD"
        });

        var servicio = CrearServicio();

        Assert.Equal("ARS", await servicio.ObtenerMonedaAsync(ContribuyenteA));
        Assert.Equal("ARS", await servicio.ObtenerMonedaAsync(null));
    }

    [MongoFact]
    public async Task IndiceUnico_Parametros_ImpideDuplicarLaClave()
    {
        await _context!.Parametros.InsertOneAsync(new Parametro
        {
            ContribuyenteId = ContribuyenteA,
            Clave = "Moneda",
            Valor = "USD"
        });

        await Assert.ThrowsAsync<MongoWriteException>(() =>
            _context!.Parametros.InsertOneAsync(new Parametro
            {
                ContribuyenteId = ContribuyenteA,
                Clave = "Moneda",
                Valor = "EUR"
            }));
    }

    [MongoFact]
    public async Task IndiceUnico_Parametros_ImpideDuplicarElValorGlobal()
    {
        await _context!.Parametros.InsertOneAsync(new Parametro
        {
            ContribuyenteId = null,
            Clave = "PaginaMaxima",
            Valor = "100"
        });

        await Assert.ThrowsAsync<MongoWriteException>(() =>
            _context!.Parametros.InsertOneAsync(new Parametro
            {
                ContribuyenteId = null,
                Clave = "PaginaMaxima",
                Valor = "50"
            }));
    }

    private ParametrosService CrearServicio()
        => new(new ParametrosRepository(_context!), new ParametrosFacturacion());
}
