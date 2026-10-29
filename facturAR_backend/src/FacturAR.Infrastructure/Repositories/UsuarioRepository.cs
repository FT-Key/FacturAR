using FacturAR.Application.Abstractions;
using FacturAR.Infrastructure.Mongo;
using FacturAR.Domain.Entities;
using MongoDB.Driver;

namespace FacturAR.Infrastructure.Repositories;

public sealed class UsuarioRepository : IUsuarioRepository
{
    private readonly MongoDbContext _context;

    public UsuarioRepository(MongoDbContext context)
    {
        _context = context;
    }

    public async Task<Usuario?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        var normalizado = Normalizar(email);
        return await _context.Usuarios
            .Find(u => u.Email == normalizado)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<Usuario?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _context.Usuarios
            .Find(u => u.Id == id)
            .FirstOrDefaultAsync(ct);
    }

    public async Task InsertAsync(Usuario usuario, CancellationToken ct = default)
    {
        usuario.Email = Normalizar(usuario.Email);
        usuario.CreatedAt = DateTime.UtcNow;
        await _context.Usuarios.InsertOneAsync(usuario, cancellationToken: ct);
    }

    public async Task UpdateAsync(Usuario usuario, CancellationToken ct = default)
    {
        var update = Builders<Usuario>.Update
            .Set(u => u.PasswordHash, usuario.PasswordHash)
            .Set(u => u.Rol, usuario.Rol)
            .Set(u => u.Activo, usuario.Activo)
            .Set(u => u.UltimoAcceso, usuario.UltimoAcceso)
            .Set(u => u.EmailVerificadoAt, usuario.EmailVerificadoAt);

        await _context.Usuarios.UpdateOneAsync(
            u => u.Id == usuario.Id,
            update,
            cancellationToken: ct);
    }

    private static string Normalizar(string email) => email.Trim().ToLowerInvariant();
}