using FacturAR.Application.ApiKeys;
using FacturAR.Application.Parametros;
using FacturAR.Application.Seed;
using FacturAR.Domain.Entities;
using FacturAR.Domain.Enums;
using FacturAR.Infrastructure.Mongo;
using FacturAR.Infrastructure.Password;
using FacturAR.Infrastructure.Repositories;
using MongoDB.Driver;

namespace FacturAR.IntegrationTests;

public sealed class MongoSeedTests : IAsyncLifetime
{
    private const string PasswordDemo = "DemoSegura2026";

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
    public async Task CP1806_SeedCreaContribuyenteUsuariosAdministradorPuntoVentaSecuenciasYParametros()
    {
        var resultado = await CrearServicio().EjecutarAsync(SeedEnvironment.Production);

        Assert.True(resultado.Ejecutado);
        Assert.Null(resultado.Error);
        Assert.Null(resultado.ApiKeyDemoValor);

        var contribuyente = await _context!.Contribuyentes
            .Find(FilterDefinition<Contribuyente>.Empty)
            .SingleAsync();
        Assert.Equal("20123456786", contribuyente.Cuit);
        Assert.Equal("Empresa Demo (simulada)", contribuyente.RazonSocial);
        Assert.Equal(CondicionIva.ResponsableInscripto, contribuyente.CondicionIva);
        Assert.True(contribuyente.Activo);

        var usuarios = await _context.Usuarios.Find(FilterDefinition<Usuario>.Empty).ToListAsync();
        Assert.Equal(3, usuarios.Count);
        Assert.Contains(usuarios, u => u.Rol == RolUsuario.Administrador && u.Email == "admin@demo.facturar.local");
        Assert.Contains(usuarios, u => u.Rol == RolUsuario.Emisor && u.Email == "admin+emisor@demo.facturar.local");
        Assert.Contains(usuarios, u => u.Rol == RolUsuario.Consulta && u.Email == "admin+consulta@demo.facturar.local");

        var administrador = await _context.Administradores
            .Find(FilterDefinition<AdministradorFacturar>.Empty)
            .SingleAsync();
        Assert.Equal(RolGlobal.SuperAdmin, administrador.Rol);
        Assert.Equal("admin+superadmin@demo.facturar.local", administrador.Email);

        var puntoVenta = await _context.PuntosVenta
            .Find(FilterDefinition<PuntoVenta>.Empty)
            .SingleAsync();
        Assert.Equal(1, puntoVenta.Numero);
        Assert.True(puntoVenta.Activo);

        var secuencias = await _context.Secuencias.Find(FilterDefinition<Secuencia>.Empty).ToListAsync();
        Assert.Equal(3, secuencias.Count);
        Assert.All(secuencias, s => Assert.Equal(0, s.UltimoNumero));

        var parametros = await _context.Parametros
            .Find(Builders<Parametro>.Filter.Eq(p => p.ContribuyenteId, null))
            .ToListAsync();
        Assert.Equal(ParametrosFacturacion.Claves.Count, parametros.Count);
        Assert.Contains(
            parametros,
            p => p.Clave == nameof(ParametrosFacturacion.Moneda)
                && p.Valor == ParametrosFacturacion.MonedaPredeterminada);
    }

    [MongoFact]
    public async Task CP1807_SegundaEjecucion_NoDuplicaDatos()
    {
        var servicio = CrearServicio();
        await servicio.EjecutarAsync(SeedEnvironment.Production);
        await servicio.EjecutarAsync(SeedEnvironment.Production);

        Assert.Equal(1, await _context!.Contribuyentes.CountDocumentsAsync(FilterDefinition<Contribuyente>.Empty));
        Assert.Equal(3, await _context.Usuarios.CountDocumentsAsync(FilterDefinition<Usuario>.Empty));
        Assert.Equal(1, await _context.Administradores.CountDocumentsAsync(FilterDefinition<AdministradorFacturar>.Empty));
        Assert.Equal(1, await _context.PuntosVenta.CountDocumentsAsync(FilterDefinition<PuntoVenta>.Empty));
        Assert.Equal(3, await _context.Secuencias.CountDocumentsAsync(FilterDefinition<Secuencia>.Empty));
        Assert.Equal(
            ParametrosFacturacion.Claves.Count,
            await _context.Parametros.CountDocumentsAsync(FilterDefinition<Parametro>.Empty));
    }

    [MongoFact]
    public async Task CP1808_SecuenciaExistente_NoSeReinicia()
    {
        var servicio = CrearServicio();
        await servicio.EjecutarAsync(SeedEnvironment.Production);

        var contribuyente = await _context!.Contribuyentes
            .Find(FilterDefinition<Contribuyente>.Empty)
            .SingleAsync();
        var filtro = Builders<Secuencia>.Filter.Eq(s => s.ContribuyenteId, contribuyente.Id)
            & Builders<Secuencia>.Filter.Eq(s => s.TipoComprobante, TipoComprobante.FacturaA);
        await _context.Secuencias.UpdateOneAsync(
            filtro,
            Builders<Secuencia>.Update.Set(s => s.UltimoNumero, 7L));

        await servicio.EjecutarAsync(SeedEnvironment.Production);

        var secuencia = await _context.Secuencias.Find(filtro).SingleAsync();
        Assert.Equal(7, secuencia.UltimoNumero);
    }

    [MongoFact]
    public async Task CP1809_SeedDeshabilitado_NoCreaDatos()
    {
        var resultado = await CrearServicio(new SeedOptions { Enabled = false, Password = PasswordDemo })
            .EjecutarAsync(SeedEnvironment.Development);

        Assert.False(resultado.Ejecutado);
        Assert.Equal(0, await _context!.Contribuyentes.CountDocumentsAsync(FilterDefinition<Contribuyente>.Empty));
        Assert.Equal(0, await _context.Usuarios.CountDocumentsAsync(FilterDefinition<Usuario>.Empty));
        Assert.Equal(0, await _context.Administradores.CountDocumentsAsync(FilterDefinition<AdministradorFacturar>.Empty));
        Assert.Equal(0, await _context.Parametros.CountDocumentsAsync(FilterDefinition<Parametro>.Empty));
    }

    [MongoFact]
    public async Task CP1810_ProductionConSeedDeshabilitado_NoCreaDatos()
    {
        var resultado = await CrearServicio(new SeedOptions { Enabled = false, Password = PasswordDemo })
            .EjecutarAsync(SeedEnvironment.Production);

        Assert.False(resultado.Ejecutado);
        Assert.Equal(0, await _context!.Contribuyentes.CountDocumentsAsync(FilterDefinition<Contribuyente>.Empty));
    }

    [MongoFact]
    public async Task CP1811_ProductionConBanderaExplicita_CargaElSeed()
    {
        var resultado = await CrearServicio(new SeedOptions { Enabled = true, Password = PasswordDemo })
            .EjecutarAsync(SeedEnvironment.Production);

        Assert.True(resultado.Ejecutado);
        Assert.Null(resultado.Error);
        Assert.Equal(1, await _context!.Contribuyentes.CountDocumentsAsync(FilterDefinition<Contribuyente>.Empty));
        Assert.Equal(1, await _context.Administradores.CountDocumentsAsync(FilterDefinition<AdministradorFacturar>.Empty));
    }

    [MongoFact]
    public async Task CP1812_DevelopmentSinApiKeyDemo_NoCreaApiKey()
    {
        var resultado = await CrearServicio(new SeedOptions
        {
            Enabled = true,
            Password = PasswordDemo,
            ApiKeyDemo = false
        }).EjecutarAsync(SeedEnvironment.Development);

        Assert.True(resultado.Ejecutado);
        Assert.Null(resultado.ApiKeyDemoValor);
        Assert.Equal(0, await _context!.ApiKeys.CountDocumentsAsync(FilterDefinition<ApiKey>.Empty));
    }

    [MongoFact]
    public async Task CP1813_DevelopmentConApiKeyDemo_CreaLaKeyYElValorSeEntregaUnaVez()
    {
        var servicio = CrearServicio(new SeedOptions
        {
            Enabled = true,
            Password = PasswordDemo,
            ApiKeyDemo = true
        });

        var primera = await servicio.EjecutarAsync(SeedEnvironment.Development);
        var segunda = await servicio.EjecutarAsync(SeedEnvironment.Development);

        Assert.True(primera.ApiKeyDemoCreada);
        Assert.NotNull(primera.ApiKeyDemoValor);
        Assert.False(segunda.ApiKeyDemoCreada);
        Assert.Null(segunda.ApiKeyDemoValor);

        var apiKey = await _context!.ApiKeys.Find(FilterDefinition<ApiKey>.Empty).SingleAsync();
        Assert.Equal(SeedService.NombreApiKeyDemo, apiKey.Nombre);
        Assert.NotEqual(primera.ApiKeyDemoValor, apiKey.Hash);
        Assert.True(new ApiKeyHasher().Matches(primera.ApiKeyDemoValor!, apiKey.Hash));
        Assert.Equal(1, await _context.ApiKeys.CountDocumentsAsync(FilterDefinition<ApiKey>.Empty));
    }

    [MongoFact]
    public async Task CP1814_PasswordHashEstaHasheadaYNoSeGuardaEnTextoPlano()
    {
        await CrearServicio().EjecutarAsync(SeedEnvironment.Production);

        var hasher = new PasswordHasher(1000);
        var usuarios = await _context!.Usuarios.Find(FilterDefinition<Usuario>.Empty).ToListAsync();
        Assert.All(usuarios, u => Assert.NotEqual(PasswordDemo, u.PasswordHash));
        Assert.All(usuarios, u => Assert.True(hasher.Verify(PasswordDemo, u.PasswordHash)));

        var administrador = await _context.Administradores
            .Find(FilterDefinition<AdministradorFacturar>.Empty)
            .SingleAsync();
        Assert.NotEqual(PasswordDemo, administrador.PasswordHash);
        Assert.True(hasher.Verify(PasswordDemo, administrador.PasswordHash));
    }

    [MongoFact]
    public async Task CP1815_ParametrosGlobalesPersistidosConUpdatedAtUtc()
    {
        await CrearServicio().EjecutarAsync(SeedEnvironment.Production);

        var parametros = await _context!.Parametros.Find(FilterDefinition<Parametro>.Empty).ToListAsync();
        Assert.Equal(ParametrosFacturacion.Claves.Count, parametros.Count);
        Assert.All(parametros, p => Assert.Null(p.ContribuyenteId));
        Assert.All(parametros, p => Assert.Equal(DateTimeKind.Utc, p.UpdatedAt.Kind));
        Assert.Equal("10", parametros.Single(p => p.Clave == "DiasVencimientoCae").Valor);
        Assert.Equal("ARS", parametros.Single(p => p.Clave == "Moneda").Valor);
    }

    private SeedService CrearServicio(SeedOptions? options = null, ParametrosFacturacion? parametros = null)
        => new(
            options ?? new SeedOptions { Enabled = true, Password = PasswordDemo },
            parametros ?? new ParametrosFacturacion(),
            new SeedRepository(_context!),
            new PasswordHasher(1000),
            new ApiKeyGenerator(new ApiKeysOptions()),
            new ApiKeyHasher());
}
