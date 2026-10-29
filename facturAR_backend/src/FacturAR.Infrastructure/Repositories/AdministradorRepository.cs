using FacturAR.Application.Abstractions;
using FacturAR.Infrastructure.Mongo;
using FacturAR.Domain.Entities;
using MongoDB.Driver;

namespace FacturAR.Infrastructure.Repositories;

public sealed class AdministradorRepository : IAdministradorRepository
{
    private readonly MongoDbContext _context;

    public AdministradorRepository(MongoDbContext context)
    {
        _context = context;
    }

    public async Task<AdministradorFacturar?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        var normalizado = Normalizar(email);
        return await _context.Administradores
            .Find(a => a.Email == normalizado)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<AdministradorFacturar?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Administradores
            .Find(a => a.Id == id)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default)
    {
        var normalizado = Normalizar(email);
        return await _context.Administradores
            .Find(a => a.Email == normalizado)
            .AnyAsync(ct);
    }

    public async Task InsertAsync(AdministradorFacturar administrador, CancellationToken ct = default)
    {
        administrador.Email = Normalizar(administrador.Email);
        administrador.CreatedAt = DateTime.UtcNow;
        await _context.Administradores.InsertOneAsync(administrador, cancellationToken: ct);
    }

    public async Task UpdateAsync(AdministradorFacturar administrador, CancellationToken ct = default)
    {
        var update = Builders<AdministradorFacturar>.Update
            .Set(a => a.Nombre, administrador.Nombre)
            .Set(a => a.PasswordHash, administrador.PasswordHash)
            .Set(a => a.Rol, administrador.Rol)
            .Set(a => a.Activo, administrador.Activo)
            .Set(a => a.UltimoAcceso, administrador.UltimoAcceso);

        await _context.Administradores.UpdateOneAsync(
            a => a.Id == administrador.Id,
            update,
            cancellationToken: ct);
    }

    private static string Normalizar(string email) => email.Trim().ToLowerInvariant();
}