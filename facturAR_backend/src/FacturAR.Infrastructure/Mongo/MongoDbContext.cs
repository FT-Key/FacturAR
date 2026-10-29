using FacturAR.Domain.Entities;
using MongoDB.Driver;

namespace FacturAR.Infrastructure.Mongo;

public sealed class MongoDbContext
{
    private readonly IMongoDatabase _database;

    public MongoDbContext(IMongoDatabase database)
    {
        _database = database;
    }

    public IMongoCollection<Contribuyente> Contribuyentes
        => _database.GetCollection<Contribuyente>(MongoCollections.Contribuyentes);

    public IMongoCollection<Usuario> Usuarios
        => _database.GetCollection<Usuario>(MongoCollections.Usuarios);

    public IMongoCollection<AdministradorFacturar> Administradores
        => _database.GetCollection<AdministradorFacturar>(MongoCollections.Administradores);

    public IMongoCollection<ApiKey> ApiKeys
        => _database.GetCollection<ApiKey>(MongoCollections.ApiKeys);

    public IMongoCollection<PuntoVenta> PuntosVenta
        => _database.GetCollection<PuntoVenta>(MongoCollections.PuntosVenta);

    public IMongoCollection<Secuencia> Secuencias
        => _database.GetCollection<Secuencia>(MongoCollections.Secuencias);

    public IMongoCollection<Comprobante> Comprobantes
        => _database.GetCollection<Comprobante>(MongoCollections.Comprobantes);

    public IMongoCollection<OutboxMessage> Outbox
        => _database.GetCollection<OutboxMessage>(MongoCollections.Outbox);

    public IMongoCollection<EventoAuditoria> Eventos
        => _database.GetCollection<EventoAuditoria>(MongoCollections.Eventos);
}
