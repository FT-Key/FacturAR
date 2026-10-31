using FacturAR.Application.Abstractions;
using FacturAR.Domain.Entities;
using FacturAR.Domain.Enums;
using FacturAR.Infrastructure.Mongo;
using MongoDB.Driver;

namespace FacturAR.Infrastructure.Repositories;

public sealed class SecuenciaRepository(MongoDbContext context, MongoSesionActual sesion) : ISecuenciaRepository
{
    private static readonly FilterDefinitionBuilder<Secuencia> Filtro = Builders<Secuencia>.Filter;
    private static readonly UpdateDefinitionBuilder<Secuencia> Update = Builders<Secuencia>.Update;

    public async Task<long> ObtenerSiguienteNumeroAsync(
        Guid contribuyenteId,
        int puntoVenta,
        TipoComprobante tipo,
        CancellationToken ct = default)
    {
        if (sesion.Sesion is null)
        {
            throw new InvalidOperationException("La asignación de número requiere una transacción activa.");
        }

        var filtro = Filtro.Eq(s => s.ContribuyenteId, contribuyenteId)
            & Filtro.Eq(s => s.PuntoVenta, puntoVenta)
            & Filtro.Eq(s => s.TipoComprobante, tipo);

        var actualizacion = Update
            .Inc(s => s.UltimoNumero, 1L)
            .SetOnInsert(s => s.Id, Guid.NewGuid());

        var opciones = new FindOneAndUpdateOptions<Secuencia>
        {
            ReturnDocument = ReturnDocument.After,
            IsUpsert = true
        };

        var documento = await sesion.FindOneAndUpdateAsync(context.Secuencias, filtro, actualizacion, opciones, ct);

        return documento is null
            ? throw new InvalidOperationException("No se pudo asignar el número de secuencia.")
            : documento.UltimoNumero;
    }
}
