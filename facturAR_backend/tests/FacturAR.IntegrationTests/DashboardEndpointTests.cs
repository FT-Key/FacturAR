using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FacturAR.Contracts.Auth;
using FacturAR.Contracts.Comunes;
using FacturAR.Contracts.Comprobantes;
using FacturAR.Domain.Entities;
using FacturAR.Domain.Enums;
using Microsoft.AspNetCore.Mvc.Testing;
using MongoDB.Driver;
using MongoDB.Driver.Core.Clusters;
using MongoDB.Driver.Core.Connections;
using MongoDB.Driver.Core.Servers;

namespace FacturAR.IntegrationTests;

public sealed class DashboardEndpointTests
{
    private const string Ruta = "/v1/comprobantes/dashboard";

    [Fact]
    public async Task Dashboard_SinApiKeyNiCookie_Devuelve401YNoConsultaRepositorios()
    {
        using var factory = new ComprobanteConsultaFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(Ruta);

        await AssertErrorAsync(response, HttpStatusCode.Unauthorized, "API_KEY_AUSENTE");
        Assert.Equal(0, factory.Comprobantes.ConsultasMetricas);
        Assert.Equal(0, factory.Comprobantes.ConsultasListado);
        Assert.Equal(0, factory.Secuencias.Consultas);
    }

    [Fact]
    public async Task Dashboard_ConApiKeyValida_DevuelveMetricasDelContribuyenteYDelMes()
    {
        using var factory = new ComprobanteConsultaFactory();
        var contribuyenteA = ComprobanteConsultaFactory.ContribuyenteId;
        var contribuyenteB = Guid.Parse("77777777-7777-7777-7777-777777777777");
        const string caeReciente = "99999999999999";

        factory.Comprobantes.Comprobantes.AddRange(
        [
            NuevoComprobante(contribuyenteA, TipoComprobante.FacturaC, 0, Utc(2026, 10, 5), importeTotal: 400m),
            NuevoComprobante(contribuyenteA, TipoComprobante.FacturaC, 1, Utc(2026, 10, 15), importeTotal: 1000m),
            NuevoComprobante(contribuyenteA, TipoComprobante.FacturaC, 2, Utc(2026, 10, 20), importeTotal: 2000m),
            NuevoComprobante(
                contribuyenteA,
                TipoComprobante.FacturaC,
                3,
                Utc(2026, 10, 25),
                estado: EstadoComprobante.Anulado,
                importeTotal: 5000m),
            NuevoComprobante(contribuyenteA, TipoComprobante.FacturaC, 4, Utc(2026, 9, 10), importeTotal: 7000m),
            NuevoComprobante(
                contribuyenteA,
                TipoComprobante.FacturaC,
                5,
                Utc(2026, 11, 15),
                importeTotal: 8000m,
                cae: caeReciente),
            NuevoComprobante(contribuyenteB, TipoComprobante.FacturaC, 6, Utc(2026, 10, 18), importeTotal: 9999m)
        ]);
        factory.Secuencias.MaximoUltimoNumero = 77;
        using var client = factory.CreateClient();

        var response = await EnviarConApiKeyAsync(client, $"{Ruta}?mes=2026-10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dashboard = await LeerAsync<DashboardResponse>(response);

        Assert.Equal(Utc(2026, 10, 1), dashboard.Periodo.Desde);
        Assert.Equal(Utc(2026, 11, 1), dashboard.Periodo.Hasta);
        Assert.Equal(3, dashboard.CantidadComprobantes);
        Assert.Equal(3400m, dashboard.ImporteTotal);
        Assert.Equal(caeReciente, dashboard.UltimoCae);
        Assert.Equal(77, dashboard.UltimoNumeroAutorizado);
        Assert.Equal(5, dashboard.UltimosComprobantes.Count);
        Assert.Equal(5, dashboard.UltimosComprobantes[0].Numero);
        Assert.Equal(contribuyenteA, factory.Comprobantes.UltimoContribuyenteMetricas);
        Assert.Equal(Utc(2026, 10, 1), factory.Comprobantes.UltimoDesdeMetricas);
        Assert.Equal(Utc(2026, 11, 1), factory.Comprobantes.UltimoHastaMetricas);

        var json = await response.Content.ReadAsStringAsync();
        AssertNoExponeCamposInternos(json);
    }

    [Theory]
    [InlineData("2026-13")]
    [InlineData("2026-1")]
    [InlineData("10-2026")]
    [InlineData("texto")]
    [InlineData("2026-10-01")]
    public async Task Dashboard_ConMesInvalido_Devuelve400SinConsultarRepositorios(string mes)
    {
        using var factory = new ComprobanteConsultaFactory();
        using var client = factory.CreateClient();

        var response = await EnviarConApiKeyAsync(
            client,
            $"{Ruta}?mes={Uri.EscapeDataString(mes)}");

        await AssertErrorAsync(response, HttpStatusCode.BadRequest, "VALIDACION");
        Assert.Equal(0, factory.Comprobantes.ConsultasMetricas);
        Assert.Equal(0, factory.Comprobantes.ConsultasListado);
        Assert.Equal(0, factory.Secuencias.Consultas);
    }

    [Fact]
    public async Task Dashboard_NoSeInterpretaComoUnIdentificadorDeComprobante()
    {
        using var factory = new ComprobanteConsultaFactory();
        factory.Comprobantes.Comprobantes.Add(
            NuevoComprobante(ComprobanteConsultaFactory.ContribuyenteId, TipoComprobante.FacturaC, 1, Utc(2026, 10, 15)));
        using var client = factory.CreateClient();

        var response = await EnviarConApiKeyAsync(client, Ruta);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dashboard = await LeerAsync<DashboardResponse>(response);
        Assert.Equal(1, dashboard.Periodo.Desde.Day);
        Assert.Equal(0, factory.Comprobantes.ConsultasDetalle);
    }

    [Fact]
    public async Task Dashboard_ConCookieDeEmpresaRolConsulta_Devuelve200()
    {
        using var factory = new ComprobanteConsultaFactory();
        factory.Comprobantes.Comprobantes.Add(
            NuevoComprobante(ComprobanteConsultaFactory.ContribuyenteId, TipoComprobante.FacturaC, 1, Utc(2026, 10, 15)));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false,
            AllowAutoRedirect = false
        });

        var login = await client.PostAsJsonAsync(
            "/v1/auth/login",
            new LoginRequest(ComprobanteConsultaFactory.EmailUsuario, ComprobanteConsultaFactory.PasswordUsuario));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var cookie = login.Headers.GetValues("Set-Cookie").First().Split(';')[0];

        using var mensaje = new HttpRequestMessage(HttpMethod.Get, $"{Ruta}?mes=2026-10");
        mensaje.Headers.TryAddWithoutValidation("Cookie", cookie);

        var response = await client.SendAsync(mensaje);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dashboard = await LeerAsync<DashboardResponse>(response);
        Assert.Equal(1, dashboard.CantidadComprobantes);
        Assert.Equal(ComprobanteConsultaFactory.ContribuyenteId, factory.Comprobantes.UltimoContribuyenteMetricas);
    }

    [Fact]
    public async Task Listar_ConFiltroDeTipoYPageDos_MantieneElFiltro()
    {
        using var factory = new ComprobanteConsultaFactory();
        var contribuyente = ComprobanteConsultaFactory.ContribuyenteId;
        factory.Comprobantes.Comprobantes.AddRange(
        [
            NuevoComprobante(contribuyente, TipoComprobante.FacturaC, 1, Utc(2026, 10, 10)),
            NuevoComprobante(contribuyente, TipoComprobante.FacturaC, 2, Utc(2026, 10, 11)),
            NuevoComprobante(contribuyente, TipoComprobante.FacturaC, 3, Utc(2026, 10, 12)),
            NuevoComprobante(contribuyente, TipoComprobante.FacturaA, 4, Utc(2026, 10, 13))
        ]);
        using var client = factory.CreateClient();

        var response = await EnviarConApiKeyAsync(client, "/v1/comprobantes?tipo=11&page=2&pageSize=2");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var pagina = await LeerAsync<PaginaResponse<ComprobanteResumenResponse>>(response);
        Assert.Equal(3, pagina.Total);
        Assert.Equal(2, pagina.Page);
        Assert.Equal(2, pagina.PageSize);
        var item = Assert.Single(pagina.Items);
        Assert.Equal(1, item.Numero);
        Assert.Equal((int)TipoComprobante.FacturaC, item.TipoComprobante);
    }

    [Fact]
    public async Task Dashboard_ConMongoNoDisponible_Devuelve503()
    {
        using var factory = new ComprobanteConsultaFactory();
        factory.Comprobantes.ExcepcionMetricas = MongoNoDisponible();
        using var client = factory.CreateClient();

        var response = await EnviarConApiKeyAsync(client, Ruta);

        await AssertErrorAsync(response, HttpStatusCode.ServiceUnavailable, "MONGO_NO_DISPONIBLE");
    }

    private static Comprobante NuevoComprobante(
        Guid contribuyenteId,
        TipoComprobante tipo,
        long numero,
        DateTime fecha,
        EstadoComprobante estado = EstadoComprobante.Emitido,
        decimal importeTotal = 12100m,
        string cae = "12345678901234")
        => new()
        {
            ContribuyenteId = contribuyenteId,
            EmisorCuit = ComprobanteConsultaFactory.CuitEmisor,
            EmisorRazonSocial = "Demo SRL",
            PuntoVenta = 1,
            TipoComprobante = tipo,
            Numero = numero,
            FechaEmision = fecha,
            Cae = cae,
            CaeVencimiento = fecha.Date.AddDays(10),
            ReceptorDocumentoTipo = "CUIT",
            ReceptorDocumentoNumero = ComprobanteConsultaFactory.CuitReceptor,
            ReceptorRazonSocial = "Cliente Final",
            ReceptorCondicionIva = CondicionIva.ResponsableInscripto,
            ImporteNeto = importeTotal,
            ImporteIva = 0m,
            ImporteTotal = importeTotal,
            Moneda = "ARS",
            Estado = estado,
            IdempotencyKey = "idem-interna-no-expuesta"
        };

    private static DateTime Utc(int anio, int mes, int dia)
        => new(anio, mes, dia, 0, 0, 0, DateTimeKind.Utc);

    private static MongoConnectionException MongoNoDisponible()
        => new(
            new ConnectionId(new ServerId(new ClusterId(1), new DnsEndPoint("localhost", 27017))),
            "sin conexión de prueba.");

    private static void AssertNoExponeCamposInternos(string json)
    {
        Assert.DoesNotContain("contribuyenteId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("idempotencyKey", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hash", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("apikey", json, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<HttpResponseMessage> EnviarConApiKeyAsync(HttpClient client, string ruta)
    {
        using var mensaje = new HttpRequestMessage(HttpMethod.Get, ruta);
        mensaje.Headers.TryAddWithoutValidation("X-Api-Key", ComprobanteConsultaEndpointsTests.ApiKeyValida);

        return await client.SendAsync(mensaje);
    }

    private static async Task<T> LeerAsync<T>(HttpResponseMessage response)
        => (await response.Content.ReadFromJsonAsync<T>())
            ?? throw new InvalidOperationException($"La respuesta no contiene {typeof(T).Name}.");

    private static async Task<ErrorResponse> AssertErrorAsync(
        HttpResponseMessage response,
        HttpStatusCode status,
        string codigo)
    {
        Assert.Equal(status, response.StatusCode);
        var error = await LeerAsync<ErrorResponse>(response);
        Assert.Equal(codigo, error.Codigo);
        return error;
    }
}
