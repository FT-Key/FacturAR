using System.Net;
using System.Net.Http.Json;
using FacturAR.Application.Outbox;
using FacturAR.Contracts.Comprobantes;
using FacturAR.Domain.Enums;
using FacturAR.Infrastructure.Outbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FacturAR.IntegrationTests;

public sealed class OutboxIntegrationTests
{
    [Fact]
    public void ElWorkerSeRegistraYQuedaDeshabilitadoEnLosTests()
    {
        using var factory = new CustomWebApplicationFactory();

        var opciones = factory.Services.GetRequiredService<OutboxOptions>();

        Assert.False(opciones.Habilitado);
        Assert.Contains(factory.Services.GetServices<IHostedService>(), servicio => servicio is OutboxWorker);
    }

    [Fact]
    public void LosHandlersDeEmailDeSolicitudQuedanRegistrados()
    {
        using var factory = new CustomWebApplicationFactory();
        using var scope = factory.Services.CreateScope();

        var resolver = scope.ServiceProvider.GetRequiredService<OutboxHandlerResolver>();

        Assert.NotNull(resolver.Obtener(TiposMensajeOutbox.EmailSolicitudRecibida));
        Assert.NotNull(resolver.Obtener(TiposMensajeOutbox.EmailNuevaSolicitud));
    }

    [Fact]
    public async Task Emitir_RegistraElEventoComprobanteEmitido()
    {
        using var factory = new ComprobanteApiKeyFactory();
        using var client = factory.CreateClient();

        var request = new EmitirComprobanteRequest(
            (int)TipoComprobante.FacturaA,
            1,
            null,
            new ReceptorComprobanteRequest(
                "CUIT",
                ComprobanteApiKeyFactory.CuitReceptor,
                "Cliente S.A.",
                (int)CondicionIva.ResponsableInscripto),
            [new ItemComprobanteRequest("Servicio", 2m, 5000m, 21m)],
            12100m,
            null);

        using var mensaje = new HttpRequestMessage(HttpMethod.Post, "/v1/comprobantes")
        {
            Content = JsonContent.Create(request)
        };
        mensaje.Headers.TryAddWithoutValidation("Idempotency-Key", "idem-outbox-1");
        mensaje.Headers.TryAddWithoutValidation("X-Api-Key", ComprobanteApiKeyFactory.ApiKeyValida);

        var response = await client.SendAsync(mensaje);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var evento = Assert.Single(factory.Eventos.Eventos);
        Assert.Equal("ComprobanteEmitido", evento.Accion);
        Assert.Equal("Comprobante", evento.Entidad);
    }
}
