using FacturAR.Application.Abstractions;
using FacturAR.Application.Notificaciones;
using FacturAR.Domain.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FacturAR.IntegrationTests;

public sealed class SolicitudAltaWebApplicationFactory : WebApplicationFactory<Program>
{
    public FakeSolicitudAltaRepository Solicitudes { get; } = new();

    public FakeContribuyenteRepository Contribuyentes { get; } = new();

    public FakeEventoAuditoriaRepository Eventos { get; } = new();

    public FakeEmailSender Emails { get; } = new();

    public string AdminNotificationEmail { get; set; } = string.Empty;

    public bool ConfiarEnProxy { get; set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Mongo:InitializeIndexes", "false");
        builder.UseSetting("Admin:NotificationEmail", AdminNotificationEmail);
        builder.UseSetting("RateLimit:SolicitudAlta:ConfiarEnProxy", ConfiarEnProxy.ToString());

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ISolicitudAltaRepository>();
            services.AddSingleton<ISolicitudAltaRepository>(Solicitudes);

            services.RemoveAll<IContribuyenteRepository>();
            services.AddSingleton<IContribuyenteRepository>(Contribuyentes);

            services.RemoveAll<IEventoAuditoriaRepository>();
            services.AddSingleton<IEventoAuditoriaRepository>(Eventos);

            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Emails);
        });
    }
}

public sealed class FakeSolicitudAltaRepository : ISolicitudAltaRepository
{
    public List<SolicitudAltaEmpresa> Insertadas { get; } = [];

    public HashSet<string> CuitsPendientes { get; } = new(StringComparer.Ordinal);

    public HashSet<string> EmailsPendientes { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Exception? Excepcion { get; set; }

    public Task InsertarAsync(SolicitudAltaEmpresa solicitud, CancellationToken ct = default)
    {
        if (Excepcion is not null)
        {
            throw Excepcion;
        }

        Insertadas.Add(solicitud);
        return Task.CompletedTask;
    }

    public Task<bool> ExistePendientePorCuitAsync(string cuit, CancellationToken ct = default)
    {
        if (Excepcion is not null)
        {
            throw Excepcion;
        }

        return Task.FromResult(CuitsPendientes.Contains(cuit));
    }

    public Task<bool> ExistePendientePorEmailAsync(string email, CancellationToken ct = default)
    {
        if (Excepcion is not null)
        {
            throw Excepcion;
        }

        return Task.FromResult(EmailsPendientes.Contains(email));
    }

    public Task<SolicitudAltaEmpresa?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default)
        => throw new NotSupportedException();

    public Task<(IReadOnlyList<SolicitudAltaEmpresa> Items, long Total)> ListarAsync(
        FiltroSolicitudes filtro,
        CancellationToken ct = default)
        => throw new NotSupportedException();

    public Task<bool> ActualizarDatosPendienteAsync(SolicitudAltaEmpresa solicitud, CancellationToken ct = default)
        => throw new NotSupportedException();

    public Task<bool> AprobarPendienteAsync(
        Guid id,
        Guid contribuyenteId,
        Guid invitacionId,
        Guid administradorId,
        DateTime revisadaAt,
        CancellationToken ct = default)
        => throw new NotSupportedException();

    public Task<bool> RechazarPendienteAsync(
        Guid id,
        string motivo,
        Guid administradorId,
        DateTime revisadaAt,
        CancellationToken ct = default)
        => throw new NotSupportedException();
}

public sealed class FakeContribuyenteRepository : IContribuyenteRepository
{
    public HashSet<string> Cuits { get; } = new(StringComparer.Ordinal);

    public Task<Contribuyente?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult<Contribuyente?>(null);

    public Task<bool> ExistePorCuitAsync(string cuit, CancellationToken ct = default)
        => Task.FromResult(Cuits.Contains(cuit));

    public Task InsertarAsync(Contribuyente contribuyente, CancellationToken ct = default)
        => throw new NotSupportedException();
}

public sealed class FakeEventoAuditoriaRepository : IEventoAuditoriaRepository
{
    public List<EventoAuditoria> Eventos { get; } = [];

    public Exception? Excepcion { get; set; }

    public IReadOnlyList<string> Acciones => Eventos.Select(e => e.Accion).ToList();

    public Task AddAsync(EventoAuditoria evento, CancellationToken ct = default)
    {
        if (Excepcion is not null)
        {
            throw Excepcion;
        }

        Eventos.Add(evento);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<EventoAuditoria>> ListarPorEntidadAsync(
        string entidad,
        string entidadId,
        CancellationToken ct = default)
        => throw new NotSupportedException();
}

public sealed class FakeEmailSender : IEmailSender
{
    public List<EmailMessage> Enviados { get; } = [];

    public Exception? Excepcion { get; set; }

    public Task<EmailResult> EnviarAsync(
        EmailMessage mensaje,
        CancellationToken cancellationToken = default)
    {
        if (Excepcion is not null)
        {
            throw Excepcion;
        }

        Enviados.Add(mensaje);
        return Task.FromResult(EmailResult.Ok(DateTime.UtcNow));
    }
}
