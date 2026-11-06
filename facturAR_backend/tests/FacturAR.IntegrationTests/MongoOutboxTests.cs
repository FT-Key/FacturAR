using FacturAR.Domain;
using FacturAR.Domain.Entities;
using FacturAR.Infrastructure.Mongo;
using FacturAR.Infrastructure.Repositories;
using MongoDB.Driver;

namespace FacturAR.IntegrationTests;

public sealed class MongoOutboxTests : IAsyncLifetime
{
    private readonly string _databaseName = $"facturar_it_{Guid.NewGuid():N}"[..28];

    private IMongoClient? _mongoClient;
    private MongoDbContext? _context;
    private OutboxRepository? _outbox;

    public async Task InitializeAsync()
    {
        if (string.IsNullOrWhiteSpace(MongoTestSettings.ConnectionString))
        {
            return;
        }

        MongoSerialization.Configure();
        _mongoClient = new MongoClient(MongoTestSettings.GetConnectionStringWithTimeout());
        _context = new MongoDbContext(_mongoClient.GetDatabase(_databaseName));
        _outbox = new OutboxRepository(_context, new MongoSesionActual());

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await MongoIndexInitializer.InitializeAsync(_context, timeout.Token);
    }

    public Task DisposeAsync()
    {
        _mongoClient?.DropDatabase(_databaseName);
        return Task.CompletedTask;
    }

    [MongoFact]
    public async Task Encolar_GuardaElMensajePendienteSinSecretos()
    {
        var encolado = await _outbox!.EncolarAsync("EmailSolicitudAprobada", """{"solicitudId":"1"}""");

        var consultado = await _outbox.ConsultarAsync(encolado.Id);

        Assert.NotNull(consultado);
        Assert.Equal("EmailSolicitudAprobada", consultado.Tipo);
        Assert.Equal("""{"solicitudId":"1"}""", consultado.Payload);
        Assert.Equal(OutboxEstados.Pendiente, consultado.Estado);
        Assert.Equal(0, consultado.Intentos);
        Assert.Null(consultado.ProcesadoAt);
        Assert.Null(consultado.Error);
        Assert.Equal(DateTimeKind.Utc, consultado.CreatedAt.Kind);
    }

    [MongoFact]
    public async Task Reclamar_PasaElMensajeAProcesandoYSumaUnIntento()
    {
        var encolado = await _outbox!.EncolarAsync("Prueba", "{}");

        var reclamados = await _outbox.ReclamarPendientesAsync(10);

        var reclamado = Assert.Single(reclamados);
        Assert.Equal(encolado.Id, reclamado.Id);
        Assert.Equal(OutboxEstados.Procesando, reclamado.Estado);
        Assert.Equal(1, reclamado.Intentos);

        var guardado = await _outbox.ConsultarAsync(encolado.Id);
        Assert.Equal(OutboxEstados.Procesando, guardado!.Estado);
        Assert.Equal(1, guardado.Intentos);
    }

    [MongoFact]
    public async Task DosReclamosConcurrentes_SoloUnoObtieneElMensaje()
    {
        await _outbox!.EncolarAsync("Prueba", "{}");

        var primero = _outbox.ReclamarPendientesAsync(10);
        var segundo = _outbox.ReclamarPendientesAsync(10);
        var resultados = await Task.WhenAll(primero, segundo);

        Assert.Equal(1, resultados.Sum(r => r.Count));

        var mensajes = await _context!.Outbox.Find(FilterDefinition<OutboxMessage>.Empty).ToListAsync();
        var mensaje = Assert.Single(mensajes);
        Assert.Equal(1, mensaje.Intentos);
        Assert.Equal(OutboxEstados.Procesando, mensaje.Estado);
    }

    [MongoFact]
    public async Task UnMensajeProcesado_NoSeVuelveAReclamar()
    {
        var encolado = await _outbox!.EncolarAsync("Prueba", "{}");

        var reclamados = await _outbox.ReclamarPendientesAsync(10);
        await _outbox.MarcarProcesadoAsync(encolado.Id);

        var segundoIntento = await _outbox.ReclamarPendientesAsync(10);

        Assert.Single(reclamados);
        Assert.Empty(segundoIntento);

        var guardado = await _outbox.ConsultarAsync(encolado.Id);
        Assert.Equal(OutboxEstados.Procesado, guardado!.Estado);
        Assert.Equal(1, guardado.Intentos);
        Assert.NotNull(guardado.ProcesadoAt);
        Assert.Null(guardado.Error);
    }

    [MongoFact]
    public async Task MarcarProcesado_SoloAplicaSiElMensajeEstaProcesando()
    {
        var encolado = await _outbox!.EncolarAsync("Prueba", "{}");

        await _outbox.MarcarProcesadoAsync(encolado.Id);

        var guardado = await _outbox.ConsultarAsync(encolado.Id);
        Assert.Equal(OutboxEstados.Pendiente, guardado!.Estado);
        Assert.Null(guardado.ProcesadoAt);
    }

    [MongoFact]
    public async Task MarcarFallido_SoloAplicaSiElMensajeEstaProcesando()
    {
        var encolado = await _outbox!.EncolarAsync("Prueba", "{}");

        await _outbox.MarcarFallidoAsync(encolado.Id, "error");

        var guardado = await _outbox.ConsultarAsync(encolado.Id);
        Assert.Equal(OutboxEstados.Pendiente, guardado!.Estado);
        Assert.Null(guardado.Error);
    }

    [MongoFact]
    public async Task DevolverAPendiente_RegistraElErrorYHabilitaElReintento()
    {
        var encolado = await _outbox!.EncolarAsync("Prueba", "{}");

        await _outbox.ReclamarPendientesAsync(10);
        await _outbox.DevolverAPendienteAsync(encolado.Id, "falló el handler");

        var guardado = await _outbox.ConsultarAsync(encolado.Id);
        Assert.Equal(OutboxEstados.Pendiente, guardado!.Estado);
        Assert.Equal("falló el handler", guardado.Error);
        Assert.Equal(1, guardado.Intentos);

        var reclamados = await _outbox.ReclamarPendientesAsync(10);
        Assert.Single(reclamados);
        Assert.Equal(2, reclamados[0].Intentos);
    }

    [MongoFact]
    public async Task MarcarFallido_DejaElMensajeDisponibleParaRevision()
    {
        var encolado = await _outbox!.EncolarAsync("Prueba", "{}");

        await _outbox.ReclamarPendientesAsync(10);
        await _outbox.MarcarFallidoAsync(encolado.Id, "superó el máximo de intentos");

        var guardado = await _outbox.ConsultarAsync(encolado.Id);
        Assert.Equal(OutboxEstados.Fallido, guardado!.Estado);
        Assert.Equal("superó el máximo de intentos", guardado.Error);

        var reclamados = await _outbox.ReclamarPendientesAsync(10);
        Assert.Empty(reclamados);
    }

    [MongoFact]
    public async Task ListarPorEstadoYFecha_DevuelveSoloElConjuntoEsperado()
    {
        var baseFecha = new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);

        await _context!.Outbox.InsertManyAsync(
        [
            Mensaje(OutboxEstados.Pendiente, baseFecha.AddHours(-2)),
            Mensaje(OutboxEstados.Pendiente, baseFecha),
            Mensaje(OutboxEstados.Procesado, baseFecha),
            Mensaje(OutboxEstados.Fallido, baseFecha.AddHours(2))
        ]);

        var pendientes = await _outbox!.ListarPorEstadoAsync(
            OutboxEstados.Pendiente,
            baseFecha.AddHours(-1),
            baseFecha.AddHours(1),
            10);

        var mensaje = Assert.Single(pendientes);
        Assert.Equal(OutboxEstados.Pendiente, mensaje.Estado);
        Assert.Equal(baseFecha, mensaje.CreatedAt);

        var todos = await _outbox.ListarPorEstadoAsync(OutboxEstados.Pendiente, null, null, 10);
        Assert.Equal(2, todos.Count);
    }

    [MongoFact]
    public async Task ElIndiceDeEstadoYFechaExiste()
    {
        using var indices = await _context!.Outbox.Indexes.ListAsync();
        var nombres = await indices.ToListAsync();

        Assert.Contains(nombres, i => i["name"].AsString == "ix_outbox_estado_fecha");
    }

    private static OutboxMessage Mensaje(string estado, DateTime createdAt)
        => new()
        {
            Tipo = "Prueba",
            Payload = "{}",
            Estado = estado,
            CreatedAt = createdAt
        };
}
