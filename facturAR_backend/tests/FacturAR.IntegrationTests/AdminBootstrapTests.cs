using FacturAR.Domain.Entities;
using FacturAR.Domain.Enums;
using FacturAR.Infrastructure.Mongo;
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
        var mongoClient = new MongoClient(MongoTestSettings.GetConnectionStringWithTimeout());
        var administradores = mongoClient
            .GetDatabase(databaseName)
            .GetCollection<AdministradorFacturar>(MongoCollections.Administradores);

        try
        {
            using (var primera = CrearFactory(databaseName))
            {
                using var client = primera.CreateClient();
                var creados = await administradores.CountDocumentsAsync(_ => true);
                Assert.Equal(1, creados);
            }

            var inicial = await administradores.Find(_ => true).SingleAsync();
            Assert.Equal(BootstrapEmail, inicial.Email);
            Assert.Equal(BootstrapNombre, inicial.Nombre);
            Assert.Equal(RolGlobal.SuperAdmin, inicial.Rol);
            Assert.True(inicial.Activo);
            Assert.StartsWith("pbkdf2-sha256$", inicial.PasswordHash);

            using (var segunda = CrearFactory(databaseName))
            {
                using var client = segunda.CreateClient();
                var restantes = await administradores.CountDocumentsAsync(_ => true);
                Assert.Equal(1, restantes);
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
        var mongoClient = new MongoClient(MongoTestSettings.GetConnectionStringWithTimeout());
        var administradores = mongoClient
            .GetDatabase(databaseName)
            .GetCollection<AdministradorFacturar>(MongoCollections.Administradores);

        try
        {
            using var factory = new ConfiguredWebApplicationFactory(new Dictionary<string, string>
            {
                ["Mongo:ConnectionString"] = MongoTestSettings.GetConnectionStringWithTimeout(),
                ["Mongo:DatabaseName"] = databaseName,
                ["Mongo:InitializeIndexes"] = "true",
                ["BootstrapAdmin:Email"] = string.Empty
            });

            using var client = factory.CreateClient();

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
}