using FacturAR.Domain.Entities;
using FacturAR.Domain.Enums;
using FacturAR.Infrastructure.Mongo;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace FacturAR.IntegrationTests;

public sealed class AdminBootstrapTests
{
    private const string BootstrapEmail = "root@facturar.ar";
    private const string BootstrapNombre = "Root FacturAR";
    private const string BootstrapPassword = "SuperSecreta123!";

    [MongoFact]
    public async Task Bootstrap_CreaElSuperAdminUnaSolaVezYNoPisaLaCuenta()
    {
        var databaseName = $"facturar_it_{Guid.NewGuid():N}"[..28];

        using var primera = CrearFactory(databaseName);
        using var primerCliente = primera.CreateClient();
        var mongoClient = primera.Services.GetRequiredService<IMongoClient>();
        var administradores = Administradores(mongoClient, databaseName);

        try
        {
            Assert.Equal(1, await administradores.CountDocumentsAsync(_ => true));

            var inicial = await administradores.Find(_ => true).SingleAsync();
            Assert.Equal(BootstrapEmail, inicial.Email);
            Assert.Equal(BootstrapNombre, inicial.Nombre);
            Assert.Equal(RolGlobal.SuperAdmin, inicial.Rol);
            Assert.True(inicial.Activo);
            Assert.StartsWith("pbkdf2-sha256$", inicial.PasswordHash);

            using (var segunda = CrearFactory(databaseName))
            {
                using var segundoCliente = segunda.CreateClient();
                Assert.Equal(1, await administradores.CountDocumentsAsync(_ => true));
            }

            var final = await administradores.Find(_ => true).SingleAsync();
            Assert.Equal(inicial.Id, final.Id);
            Assert.Equal(inicial.PasswordHash, final.PasswordHash);

            var cursor = await administradores.Indexes.ListAsync();
            var indices = await cursor.ToListAsync();
            Assert.Contains(indices, indice =>
                indice.TryGetValue("name", out var nombre)
                && nombre.IsString
                && nombre.AsString == "ux_administradores_email"
                && indice.TryGetValue("unique", out var unico)
                && unico.IsBoolean
                && unico.AsBoolean);
        }
        finally
        {
            await mongoClient.DropDatabaseAsync(databaseName);
        }
    }

    [MongoFact]
    public async Task Bootstrap_SinEmailConfigurado_NoCreaNingunAdministrador()
    {
        var databaseName = $"facturar_it_{Guid.NewGuid():N}"[..28];

        using var factory = new ConfiguredWebApplicationFactory(new Dictionary<string, string>
        {
            ["Mongo:ConnectionString"] = MongoTestSettings.GetConnectionStringWithTimeout(),
            ["Mongo:DatabaseName"] = databaseName,
            ["Mongo:InitializeIndexes"] = "true",
            ["BootstrapAdmin:Email"] = string.Empty
        });

        using var client = factory.CreateClient();
        var mongoClient = factory.Services.GetRequiredService<IMongoClient>();
        var administradores = Administradores(mongoClient, databaseName);

        try
        {
            Assert.Equal(0, await administradores.CountDocumentsAsync(_ => true));
        }
        finally
        {
            await mongoClient.DropDatabaseAsync(databaseName);
        }
    }

    private static ConfiguredWebApplicationFactory CrearFactory(string databaseName)
        => new(new Dictionary<string, string>
        {
            ["Mongo:ConnectionString"] = MongoTestSettings.GetConnectionStringWithTimeout(),
            ["Mongo:DatabaseName"] = databaseName,
            ["Mongo:InitializeIndexes"] = "true",
            ["BootstrapAdmin:Email"] = BootstrapEmail,
            ["BootstrapAdmin:Nombre"] = BootstrapNombre,
            ["BootstrapAdmin:Password"] = BootstrapPassword
        });

    private static IMongoCollection<AdministradorFacturar> Administradores(
        IMongoClient mongoClient,
        string databaseName)
        => mongoClient
            .GetDatabase(databaseName)
            .GetCollection<AdministradorFacturar>(MongoCollections.Administradores);
}