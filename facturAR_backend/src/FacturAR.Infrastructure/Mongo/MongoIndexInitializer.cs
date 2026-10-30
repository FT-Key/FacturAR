using FacturAR.Domain.Entities;
using FacturAR.Domain.Enums;
using MongoDB.Driver;

namespace FacturAR.Infrastructure.Mongo;

public static class MongoIndexInitializer
{
    public static async Task InitializeAsync(MongoDbContext context, CancellationToken cancellationToken = default)
    {
        await context.Administradores.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<AdministradorFacturar>(
                Builders<AdministradorFacturar>.IndexKeys.Ascending(a => a.Email),
                new CreateIndexOptions { Unique = true, Name = "ux_administradores_email" })
        ], cancellationToken);

        await context.Contribuyentes.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<Contribuyente>(
                Builders<Contribuyente>.IndexKeys.Ascending(c => c.Cuit),
                new CreateIndexOptions { Unique = true, Name = "ux_contribuyentes_cuit" })
        ], cancellationToken);

        await context.Usuarios.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<Usuario>(
                Builders<Usuario>.IndexKeys.Ascending(u => u.ContribuyenteId).Ascending(u => u.Email),
                new CreateIndexOptions { Unique = true, Name = "ux_usuarios_contribuyente_email" })
        ], cancellationToken);

        await context.ApiKeys.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<ApiKey>(
                Builders<ApiKey>.IndexKeys.Ascending(a => a.Prefix),
                new CreateIndexOptions { Unique = true, Name = "ux_api_keys_prefix" })
        ], cancellationToken);

        await context.PuntosVenta.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<PuntoVenta>(
                Builders<PuntoVenta>.IndexKeys.Ascending(p => p.ContribuyenteId).Ascending(p => p.Numero),
                new CreateIndexOptions { Unique = true, Name = "ux_puntos_venta_contribuyente_numero" })
        ], cancellationToken);

        await context.Secuencias.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<Secuencia>(
                Builders<Secuencia>.IndexKeys
                    .Ascending(s => s.ContribuyenteId)
                    .Ascending(s => s.PuntoVenta)
                    .Ascending(s => s.TipoComprobante),
                new CreateIndexOptions { Unique = true, Name = "ux_secuencias_contribuyente_punto_tipo" })
        ], cancellationToken);

        await context.Comprobantes.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<Comprobante>(
                Builders<Comprobante>.IndexKeys.Ascending(c => c.Cae),
                new CreateIndexOptions { Unique = true, Name = "ux_comprobantes_cae" }),
            new CreateIndexModel<Comprobante>(
                Builders<Comprobante>.IndexKeys
                    .Ascending(c => c.ContribuyenteId)
                    .Ascending(c => c.PuntoVenta)
                    .Ascending(c => c.TipoComprobante)
                    .Ascending(c => c.Numero),
                new CreateIndexOptions { Unique = true, Name = "ux_comprobantes_numeracion" }),
            new CreateIndexModel<Comprobante>(
                Builders<Comprobante>.IndexKeys
                    .Ascending(c => c.ContribuyenteId)
                    .Ascending(c => c.IdempotencyKey),
                new CreateIndexOptions { Unique = true, Name = "ux_comprobantes_idempotencia" }),
            new CreateIndexModel<Comprobante>(
                Builders<Comprobante>.IndexKeys
                    .Ascending(c => c.ContribuyenteId)
                    .Descending(c => c.FechaEmision),
                new CreateIndexOptions { Name = "ix_comprobantes_contribuyente_fecha" }),
            new CreateIndexModel<Comprobante>(
                Builders<Comprobante>.IndexKeys.Ascending(c => c.ReceptorDocumentoNumero),
                new CreateIndexOptions { Name = "ix_comprobantes_receptor_documento" })
        ], cancellationToken);

        await context.Outbox.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<OutboxMessage>(
                Builders<OutboxMessage>.IndexKeys.Ascending(o => o.Estado).Ascending(o => o.CreatedAt),
                new CreateIndexOptions { Name = "ix_outbox_estado_fecha" })
        ], cancellationToken);

        await context.Eventos.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<EventoAuditoria>(
                Builders<EventoAuditoria>.IndexKeys
                    .Ascending(e => e.ContribuyenteId)
                    .Descending(e => e.CreatedAt),
                new CreateIndexOptions { Name = "ix_eventos_contribuyente_fecha" }),
            new CreateIndexModel<EventoAuditoria>(
                Builders<EventoAuditoria>.IndexKeys
                    .Ascending(e => e.AdministradorId)
                    .Descending(e => e.CreatedAt),
                new CreateIndexOptions { Name = "ix_eventos_administrador_fecha" }),
            new CreateIndexModel<EventoAuditoria>(
                Builders<EventoAuditoria>.IndexKeys
                    .Ascending(e => e.Entidad)
                    .Ascending(e => e.EntidadId)
                    .Ascending(e => e.CreatedAt),
                new CreateIndexOptions { Name = "ix_eventos_entidad_fecha" })
        ], cancellationToken);

        await context.Invitaciones.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<Invitacion>(
                Builders<Invitacion>.IndexKeys.Ascending(i => i.TokenHash),
                new CreateIndexOptions { Unique = true, Name = "ux_invitaciones_token_hash" }),
            new CreateIndexModel<Invitacion>(
                Builders<Invitacion>.IndexKeys
                    .Ascending(i => i.ContribuyenteId)
                    .Ascending(i => i.Email)
                    .Ascending(i => i.ExpiraAt),
                new CreateIndexOptions { Name = "ix_invitaciones_contribuyente_email_expira" }),
            new CreateIndexModel<Invitacion>(
                Builders<Invitacion>.IndexKeys.Ascending(i => i.ExpiraAt),
                new CreateIndexOptions { Name = "ix_invitaciones_expira" }),
            new CreateIndexModel<Invitacion>(
                Builders<Invitacion>.IndexKeys
                    .Ascending(i => i.SolicitudId)
                    .Ascending(i => i.Estado),
                new CreateIndexOptions { Name = "ix_invitaciones_solicitud_estado" })
        ], cancellationToken);

        await context.SolicitudesAlta.Indexes.CreateManyAsync(
        [
            new CreateIndexModel<SolicitudAltaEmpresa>(
                Builders<SolicitudAltaEmpresa>.IndexKeys.Ascending(s => s.Cuit),
                new CreateIndexOptions<SolicitudAltaEmpresa>
                {
                    Unique = true,
                    Name = "ux_solicitudes_alta_cuit_pendiente",
                    PartialFilterExpression = Builders<SolicitudAltaEmpresa>.Filter.Eq(
                        s => s.Estado,
                        EstadoSolicitud.Pendiente)
                }),
            new CreateIndexModel<SolicitudAltaEmpresa>(
                Builders<SolicitudAltaEmpresa>.IndexKeys.Ascending(s => s.EmailSolicitante),
                new CreateIndexOptions<SolicitudAltaEmpresa>
                {
                    Unique = true,
                    Name = "ux_solicitudes_alta_email_pendiente",
                    PartialFilterExpression = Builders<SolicitudAltaEmpresa>.Filter.Eq(
                        s => s.Estado,
                        EstadoSolicitud.Pendiente)
                }),
            new CreateIndexModel<SolicitudAltaEmpresa>(
                Builders<SolicitudAltaEmpresa>.IndexKeys
                    .Ascending(s => s.Estado)
                    .Descending(s => s.CreatedAt),
                new CreateIndexOptions { Name = "ix_solicitudes_alta_estado_fecha" })
        ], cancellationToken);
    }
}
