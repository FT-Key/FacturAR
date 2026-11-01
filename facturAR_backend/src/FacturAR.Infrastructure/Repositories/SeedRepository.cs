using FacturAR.Application.Abstractions;
using FacturAR.Domain.Entities;
using FacturAR.Infrastructure.Mongo;
using MongoDB.Driver;

namespace FacturAR.Infrastructure.Repositories;

public sealed class SeedRepository : ISeedRepository
{
    private static readonly FilterDefinitionBuilder<Contribuyente> FiltroContribuyente =
        Builders<Contribuyente>.Filter;
    private static readonly FilterDefinitionBuilder<Usuario> FiltroUsuario = Builders<Usuario>.Filter;
    private static readonly FilterDefinitionBuilder<AdministradorFacturar> FiltroAdministrador =
        Builders<AdministradorFacturar>.Filter;
    private static readonly FilterDefinitionBuilder<PuntoVenta> FiltroPuntoVenta = Builders<PuntoVenta>.Filter;
    private static readonly FilterDefinitionBuilder<Secuencia> FiltroSecuencia = Builders<Secuencia>.Filter;
    private static readonly FilterDefinitionBuilder<Parametro> FiltroParametro = Builders<Parametro>.Filter;
    private static readonly FilterDefinitionBuilder<ApiKey> FiltroApiKey = Builders<ApiKey>.Filter;

    private static readonly UpdateDefinitionBuilder<Contribuyente> UpdateContribuyente =
        Builders<Contribuyente>.Update;
    private static readonly UpdateDefinitionBuilder<Usuario> UpdateUsuario = Builders<Usuario>.Update;
    private static readonly UpdateDefinitionBuilder<AdministradorFacturar> UpdateAdministrador =
        Builders<AdministradorFacturar>.Update;
    private static readonly UpdateDefinitionBuilder<PuntoVenta> UpdatePuntoVenta = Builders<PuntoVenta>.Update;
    private static readonly UpdateDefinitionBuilder<Secuencia> UpdateSecuencia = Builders<Secuencia>.Update;
    private static readonly UpdateDefinitionBuilder<Parametro> UpdateParametro = Builders<Parametro>.Update;
    private static readonly UpdateDefinitionBuilder<ApiKey> UpdateApiKey = Builders<ApiKey>.Update;

    private readonly MongoDbContext _context;

    public SeedRepository(MongoDbContext context)
    {
        _context = context;
    }

    public async Task<Contribuyente> UpsertContribuyenteAsync(
        Contribuyente contribuyente,
        CancellationToken ct = default)
    {
        var actualizacion = UpdateContribuyente
            .SetOnInsert(c => c.Id, contribuyente.Id)
            .SetOnInsert(c => c.Cuit, contribuyente.Cuit)
            .SetOnInsert(c => c.CreatedAt, contribuyente.CreatedAt)
            .Set(c => c.RazonSocial, contribuyente.RazonSocial)
            .Set(c => c.CondicionIva, contribuyente.CondicionIva)
            .Set(c => c.Email, contribuyente.Email)
            .Set(c => c.Activo, contribuyente.Activo);

        var opciones = new FindOneAndUpdateOptions<Contribuyente>
        {
            IsUpsert = true,
            ReturnDocument = ReturnDocument.After
        };

        return await _context.Contribuyentes.FindOneAndUpdateAsync(
                FiltroContribuyente.Eq(c => c.Cuit, contribuyente.Cuit),
                actualizacion,
                opciones,
                ct)
            ?? throw new InvalidOperationException("No se pudo persistir el contribuyente demo.");
    }

    public async Task UpsertUsuarioAsync(Usuario usuario, CancellationToken ct = default)
    {
        var actualizacion = UpdateUsuario
            .SetOnInsert(u => u.Id, usuario.Id)
            .SetOnInsert(u => u.PasswordHash, usuario.PasswordHash)
            .SetOnInsert(u => u.CreatedAt, usuario.CreatedAt)
            .SetOnInsert(u => u.EmailVerificadoAt, usuario.EmailVerificadoAt)
            .Set(u => u.Rol, usuario.Rol)
            .Set(u => u.Activo, usuario.Activo);

        var filtro = FiltroUsuario.Eq(u => u.ContribuyenteId, usuario.ContribuyenteId)
            & FiltroUsuario.Eq(u => u.Email, usuario.Email);

        await _context.Usuarios.UpdateOneAsync(
            filtro,
            actualizacion,
            new UpdateOptions { IsUpsert = true },
            ct);
    }

    public async Task UpsertAdministradorAsync(
        AdministradorFacturar administrador,
        CancellationToken ct = default)
    {
        var actualizacion = UpdateAdministrador
            .SetOnInsert(a => a.Id, administrador.Id)
            .SetOnInsert(a => a.PasswordHash, administrador.PasswordHash)
            .SetOnInsert(a => a.CreatedAt, administrador.CreatedAt)
            .Set(a => a.Nombre, administrador.Nombre)
            .Set(a => a.Rol, administrador.Rol)
            .Set(a => a.Activo, administrador.Activo);

        await _context.Administradores.UpdateOneAsync(
            FiltroAdministrador.Eq(a => a.Email, administrador.Email),
            actualizacion,
            new UpdateOptions { IsUpsert = true },
            ct);
    }

    public async Task UpsertPuntoVentaAsync(PuntoVenta puntoVenta, CancellationToken ct = default)
    {
        var actualizacion = UpdatePuntoVenta
            .SetOnInsert(p => p.Id, puntoVenta.Id)
            .SetOnInsert(p => p.CreatedAt, puntoVenta.CreatedAt)
            .Set(p => p.Descripcion, puntoVenta.Descripcion)
            .Set(p => p.Activo, puntoVenta.Activo);

        var filtro = FiltroPuntoVenta.Eq(p => p.ContribuyenteId, puntoVenta.ContribuyenteId)
            & FiltroPuntoVenta.Eq(p => p.Numero, puntoVenta.Numero);

        await _context.PuntosVenta.UpdateOneAsync(
            filtro,
            actualizacion,
            new UpdateOptions { IsUpsert = true },
            ct);
    }

    public async Task UpsertSecuenciaAsync(Secuencia secuencia, CancellationToken ct = default)
    {
        var actualizacion = UpdateSecuencia
            .SetOnInsert(s => s.Id, secuencia.Id)
            .SetOnInsert(s => s.UltimoNumero, 0L);

        var filtro = FiltroSecuencia.Eq(s => s.ContribuyenteId, secuencia.ContribuyenteId)
            & FiltroSecuencia.Eq(s => s.PuntoVenta, secuencia.PuntoVenta)
            & FiltroSecuencia.Eq(s => s.TipoComprobante, secuencia.TipoComprobante);

        await _context.Secuencias.UpdateOneAsync(
            filtro,
            actualizacion,
            new UpdateOptions { IsUpsert = true },
            ct);
    }

    public async Task UpsertParametroAsync(Parametro parametro, CancellationToken ct = default)
    {
        var actualizacion = UpdateParametro
            .SetOnInsert(p => p.Id, parametro.Id)
            .Set(p => p.Valor, parametro.Valor)
            .Set(p => p.UpdatedAt, parametro.UpdatedAt);

        var filtro = FiltroParametro.Eq(p => p.ContribuyenteId, parametro.ContribuyenteId)
            & FiltroParametro.Eq(p => p.Clave, parametro.Clave);

        await _context.Parametros.UpdateOneAsync(
            filtro,
            actualizacion,
            new UpdateOptions { IsUpsert = true },
            ct);
    }

    public async Task<bool> CrearApiKeySiNoExisteAsync(ApiKey apiKey, CancellationToken ct = default)
    {
        var actualizacion = UpdateApiKey
            .SetOnInsert(a => a.Id, apiKey.Id)
            .SetOnInsert(a => a.Prefix, apiKey.Prefix)
            .SetOnInsert(a => a.Hash, apiKey.Hash)
            .SetOnInsert(a => a.Activa, apiKey.Activa)
            .SetOnInsert(a => a.CreatedAt, apiKey.CreatedAt);

        var filtro = FiltroApiKey.Eq(a => a.ContribuyenteId, apiKey.ContribuyenteId)
            & FiltroApiKey.Eq(a => a.Nombre, apiKey.Nombre);

        var resultado = await _context.ApiKeys.UpdateOneAsync(
            filtro,
            actualizacion,
            new UpdateOptions { IsUpsert = true },
            ct);

        return resultado.UpsertedId is not null;
    }
}
