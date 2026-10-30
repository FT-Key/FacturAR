using FacturAR.Application.Abstractions;
using FacturAR.Domain.Entities;
using FacturAR.Domain.Exceptions;
using FacturAR.Infrastructure.Mongo;
using MongoDB.Driver;

namespace FacturAR.Infrastructure.Repositories;

public sealed class UsuarioRepository : IUsuarioRepository
{
    private readonly MongoDbContext _context;
    private readonly MongoSesionActual _sesion;

    public UsuarioRepository(MongoDbContext context, MongoSesionActual sesion)
    {
        _context = context;
        _sesion = sesion;
    }

    public async Task<Usuario?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        var normalizado = Normalizar(email);
        return await _sesion
            .Find(_context.Usuarios, Builders<Usuario>.Filter.Eq(u => u.Email, normalizado))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<Usuario?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _sesion
            .Find(_context.Usuarios, Builders<Usuario>.Filter.Eq(u => u.Id, id))
            .FirstOrDefaultAsync(ct);
    }

    public async Task InsertAsync(Usuario usuario, CancellationToken ct = default)
    {
        usuario.Email = Normalizar(usuario.Email);
        usuario.CreatedAt = DateTime.UtcNow;
        try
        {
            await _sesion.InsertOneAsync(_context.Usuarios, usuario, ct);
        }
        catch (MongoException ex) when (MongoErrores.EsClaveDuplicada(ex))
        {
            throw new UsuarioExistenteException();
        }
    }

    public Task<bool> ExistePorContribuyenteYEmailAsync(Guid contribuyenteId, string email, CancellationToken ct = default)
        => _sesion
            .Find(
                _context.Usuarios,
                Builders<Usuario>.Filter.Eq(u => u.ContribuyenteId, contribuyenteId)
                    & Builders<Usuario>.Filter.Eq(u => u.Email, Normalizar(email)))
            .AnyAsync(ct);

    public async Task UpdateAsync(Usuario usuario, CancellationToken ct = default)
    {
        var update = Builders<Usuario>.Update
            .Set(u => u.PasswordHash, usuario.PasswordHash)
            .Set(u => u.Rol, usuario.Rol)
            .Set(u => u.Activo, usuario.Activo)
            .Set(u => u.UltimoAcceso, usuario.UltimoAcceso)
            .Set(u => u.EmailVerificadoAt, usuario.EmailVerificadoAt);

        await _sesion.UpdateOneAsync(
            _context.Usuarios,
            Builders<Usuario>.Filter.Eq(u => u.Id, usuario.Id),
            update,
            ct);
    }

    private static string Normalizar(string email) => email.Trim().ToLowerInvariant();
}
