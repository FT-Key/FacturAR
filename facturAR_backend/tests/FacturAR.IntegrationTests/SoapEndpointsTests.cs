using System.Net;
using System.Text;
using System.Xml.Linq;
using FacturAR.Application.Abstractions;
using FacturAR.Application.ApiKeys;
using FacturAR.Application.Comprobantes;
using FacturAR.Contracts.Comprobantes;
using FacturAR.Domain.Entities;
using FacturAR.Domain.Enums;
using FacturAR.Domain.Exceptions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FacturAR.IntegrationTests;

public sealed class SoapEndpointsTests
{
    private const string CuitEmisor = "20123456786";
    private const string CuitAjeno = "30712345671";

    [Fact]
    public async Task Wsdl_ExponeOperacionesYNamespaceSinCamposInternos()
    {
        using var factory = new SoapApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/soap?wsdl");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var xml = await response.Content.ReadAsStringAsync();
        Assert.Contains("FECAESolicitar", xml);
        Assert.Contains("FECAEGenerar", xml);
        Assert.Contains("http://ar.gov.afip.dif.FEV1Service/", xml);
        Assert.DoesNotContain("ContribuyenteId", xml);
        Assert.DoesNotContain("PasswordHash", xml);
        Assert.DoesNotContain("IdempotencyPayloadHash", xml);
    }

    [Fact]
    public async Task FECAESolicitar_SinApiKey_Devuelve401ApiKeyAusente()
    {
        using var factory = new SoapApiFactory();
        using var client = factory.CreateClient();

        using var response = await EnviarAsync(client, "FECAESolicitar", CuitEmisor, apiKey: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var documento = XDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("API_KEY_AUSENTE", Valor(documento, "codigo"));
        Assert.Contains("Client", Valor(documento, "faultcode"));
        Assert.StartsWith("API_KEY_AUSENTE: ", Valor(documento, "faultstring"), StringComparison.Ordinal);
        Assert.False(factory.Emision.EmitirLlamado);
    }

    [Fact]
    public async Task FECAESolicitar_ApiKeyInvalida_Devuelve401ApiKeyInvalida()
    {
        using var factory = new SoapApiFactory();
        using var client = factory.CreateClient();

        using var response = await EnviarAsync(
            client,
            "FECAESolicitar",
            CuitEmisor,
            SoapApiFactory.ApiKeyDesconocida);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var documento = XDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("API_KEY_INVALIDA", Valor(documento, "codigo"));
        Assert.DoesNotContain(SoapApiFactory.ApiKeyDesconocida, documento.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task FECAESolicitar_XmlMalFormado_Devuelve400Validacion()
    {
        using var factory = new SoapApiFactory();
        using var client = factory.CreateClient();

        using var response = await EnviarAsync(
            client,
            "FECAESolicitar",
            CuitEmisor,
            SoapApiFactory.ApiKeyValida,
            contenido: new StringContent("<soap:Envelope", Encoding.UTF8, "text/xml"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var documento = XDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("VALIDACION", Valor(documento, "codigo"));
        Assert.Contains("Client", Valor(documento, "faultcode"));
    }

    [Fact]
    public async Task FECAESolicitar_Exitosa_DelegaYDevuelveCae()
    {
        using var factory = new SoapApiFactory();
        using var client = factory.CreateClient();

        using var response = await EnviarAsync(
            client,
            "FECAESolicitar",
            CuitEmisor,
            SoapApiFactory.ApiKeyValida,
            idempotencyKey: "idem-soap-1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var documento = XDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("A", Valor(documento, "Resultado"));
        Assert.Equal("12345678901234", Valor(documento, "CodAutorizacion"));
        Assert.Equal("20261113", Valor(documento, "FchVto"));

        Assert.True(factory.Emision.EmitirLlamado);
        Assert.False(factory.Emision.ValidarLlamado);
        Assert.Equal(SoapApiFactory.ContribuyenteId, factory.Emision.UltimoContribuyenteId);
        Assert.Null(factory.Emision.UltimoRol);
        Assert.Equal("idem-soap-1", factory.Emision.UltimoIdempotencyKey);

        var emision = Assert.IsType<EmitirComprobanteRequest>(factory.Emision.UltimoRequest);
        Assert.Equal(11, emision.TipoComprobante);
        Assert.Equal(1, emision.PuntoVenta);
        Assert.Equal("99", emision.Receptor?.DocumentoTipo);
        Assert.Equal("0", emision.Receptor?.DocumentoNumero);
        Assert.Equal(5, emision.Receptor?.CondicionIva);
        var item = Assert.Single(emision.Items!);
        Assert.Equal("Servicio", item.Descripcion);
        Assert.Equal(100m, item.PrecioUnitario);
    }

    [Fact]
    public async Task FECAESolicitar_CuitDeOtraEmpresa_DevuelveRechazoDeAislamiento()
    {
        using var factory = new SoapApiFactory();
        using var client = factory.CreateClient();

        using var response = await EnviarAsync(
            client,
            "FECAESolicitar",
            CuitAjeno,
            SoapApiFactory.ApiKeyValida);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var documento = XDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("R", Valor(documento, "Resultado"));
        Assert.StartsWith("VALIDACION: ", Valor(documento, "Observaciones"), StringComparison.Ordinal);
        Assert.False(factory.Emision.EmitirLlamado);
    }

    [Fact]
    public async Task FECAESolicitar_ErrorDeNegocio_DevuelveSobreR()
    {
        using var factory = new SoapApiFactory();
        factory.Emision.ErrorEmitir = new PuntoVentaInactivoException();
        using var client = factory.CreateClient();

        using var response = await EnviarAsync(
            client,
            "FECAESolicitar",
            CuitEmisor,
            SoapApiFactory.ApiKeyValida);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var documento = XDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("R", Valor(documento, "Resultado"));
        Assert.Contains("PUNTO_VENTA_INACTIVO", Valor(documento, "Observaciones"));
    }

    [Fact]
    public async Task FECAEGenerar_Valido_NoEmiteNiPersiste()
    {
        using var factory = new SoapApiFactory();
        using var client = factory.CreateClient();

        using var response = await EnviarAsync(
            client,
            "FECAEGenerar",
            CuitEmisor,
            SoapApiFactory.ApiKeyValida);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var documento = XDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("A", Valor(documento, "Resultado"));
        Assert.True(factory.Emision.ValidarLlamado);
        Assert.False(factory.Emision.EmitirLlamado);
    }

    [Fact]
    public async Task FECAEGenerar_ErrorDeNegocio_DevuelveCodigoCanonico()
    {
        using var factory = new SoapApiFactory();
        factory.Emision.ErrorValidar = new ComprobanteValidationException("Los ítems no son válidos.");
        using var client = factory.CreateClient();

        using var response = await EnviarAsync(
            client,
            "FECAEGenerar",
            CuitEmisor,
            SoapApiFactory.ApiKeyValida);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var documento = XDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("R", Valor(documento, "Resultado"));
        Assert.Equal("VALIDACION", Valor(documento, "Error"));
        Assert.Equal("Los ítems no son válidos.", Valor(documento, "Observaciones"));
        Assert.False(factory.Emision.EmitirLlamado);
    }

    [Fact]
    public async Task SoapDeshabilitado_NoPublicaEndpoint()
    {
        using var factory = new SoapDeshabilitadoFactory();
        using var client = factory.CreateClient();

        using var wsdl = await client.GetAsync("/soap?wsdl");
        Assert.Equal(HttpStatusCode.NotFound, wsdl.StatusCode);

        using var response = await EnviarAsync(client, "FECAESolicitar", CuitEmisor, apiKey: null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<HttpResponseMessage> EnviarAsync(
        HttpClient client,
        string operacion,
        string cuit,
        string? apiKey,
        string? idempotencyKey = "idem-1",
        HttpContent? contenido = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/soap")
        {
            Content = contenido ?? SoapContent(operacion, cuit, idempotencyKey)
        };
        request.Headers.TryAddWithoutValidation(
            "SOAPAction",
            $"http://ar.gov.afip.dif.FEV1Service/{operacion}");
        if (apiKey is not null)
        {
            request.Headers.TryAddWithoutValidation("X-Api-Key", apiKey);
        }

        return await client.SendAsync(request);
    }

    private static StringContent SoapContent(string operacion, string cuit, string? idempotencyKey)
    {
        var idempotencia = string.IsNullOrWhiteSpace(idempotencyKey)
            ? string.Empty
            : $"<IdempotencyKey>{idempotencyKey}</IdempotencyKey>";

        var xml = $"""
            <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
              <soap:Body>
                <{operacion} xmlns="http://ar.gov.afip.dif.FEV1Service/">
                  <request>
                    <Cuit>{cuit}</Cuit>
                    <PtoVta>1</PtoVta>
                    <TipoComprobante>11</TipoComprobante>
                    <Concepto>1</Concepto>
                    <DocTipo>99</DocTipo>
                    <DocNro>0</DocNro>
                    <CondicionIVA>5</CondicionIVA>
                    <Items>
                      <Item>
                        <Descripcion>Servicio</Descripcion>
                        <Cantidad>1</Cantidad>
                        <PrecioUnitario>100</PrecioUnitario>
                        <AlicuotaIVA>0</AlicuotaIVA>
                      </Item>
                    </Items>
                    {idempotencia}
                  </request>
                </{operacion}>
              </soap:Body>
            </soap:Envelope>
            """;

        return new StringContent(xml, Encoding.UTF8, "text/xml");
    }

    private static string? Valor(XDocument documento, string nombre)
        => documento.Descendants().FirstOrDefault(e => e.Name.LocalName == nombre)?.Value;
}

public sealed class SoapApiFactory : WebApplicationFactory<Program>
{
    public const string ApiKeyValida = "api-key-soap-de-prueba";
    public const string ApiKeyDesconocida = "api-key-soap-desconocida";
    public const string CuitEmisor = "20123456786";

    public static readonly Guid ContribuyenteId = Guid.NewGuid();
    public static readonly Guid ApiKeyId = Guid.NewGuid();

    public SoapApiFactory()
    {
        Contribuyentes.Contribuyentes.Add(new Contribuyente
        {
            Id = ContribuyenteId,
            RazonSocial = "Demo SRL",
            Cuit = CuitEmisor,
            CondicionIva = CondicionIva.ResponsableInscripto,
            Activo = true
        });
    }

    public ComprobanteContribuyenteFake Contribuyentes { get; } = new();

    public SoapEmisionFake Emision { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Mongo:InitializeIndexes", "false");
        builder.UseSetting("Seed:Enabled", "false");
        builder.UseSetting("ApiKeys:ActualizarUltimoUso", "false");
        builder.UseSetting("BootstrapAdmin:Email", string.Empty);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IApiKeyAuthenticator>();
            services.AddSingleton<IApiKeyAuthenticator>(new ComprobanteApiKeyAuthenticatorFake(
                new Dictionary<string, ResultadoApiKey>(StringComparer.Ordinal)
                {
                    [ApiKeyValida] = new ResultadoApiKey(ApiKeyId, ContribuyenteId)
                }));

            services.RemoveAll<IContribuyenteRepository>();
            services.AddSingleton<IContribuyenteRepository>(Contribuyentes);

            services.RemoveAll<IEmitirComprobanteService>();
            services.AddSingleton<IEmitirComprobanteService>(Emision);
        });
    }
}

public sealed class SoapDeshabilitadoFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Mongo:InitializeIndexes", "false");
        builder.UseSetting("Seed:Enabled", "false");
        builder.UseSetting("BootstrapAdmin:Email", string.Empty);
        builder.UseSetting("Soap:Habilitado", "false");
    }
}

public sealed class SoapEmisionFake : IEmitirComprobanteService
{
    public ComprobanteResponse Respuesta { get; set; } = new(
        Guid.NewGuid(),
        11,
        "Factura C",
        1,
        7,
        new DateTime(2026, 11, 3, 0, 0, 0, DateTimeKind.Utc),
        "12345678901234",
        new DateTime(2026, 11, 13, 0, 0, 0, DateTimeKind.Utc),
        new ReceptorComprobanteResponse("99", "0", string.Empty, 5),
        100m,
        0m,
        100m,
        "ARS",
        "Emitido");

    public Exception? ErrorEmitir { get; set; }

    public Exception? ErrorValidar { get; set; }

    public bool EmitirLlamado { get; private set; }

    public bool ValidarLlamado { get; private set; }

    public Guid? UltimoContribuyenteId { get; private set; }

    public string? UltimoRol { get; private set; }

    public EmitirComprobanteRequest? UltimoRequest { get; private set; }

    public string? UltimoIdempotencyKey { get; private set; }

    public Task<ResultadoEmision> EmitirAsync(
        Guid contribuyenteId,
        string? rol,
        EmitirComprobanteRequest request,
        string idempotencyKey,
        CancellationToken ct = default)
    {
        EmitirLlamado = true;
        UltimoContribuyenteId = contribuyenteId;
        UltimoRol = rol;
        UltimoRequest = request;
        UltimoIdempotencyKey = idempotencyKey;

        if (ErrorEmitir is not null)
        {
            throw ErrorEmitir;
        }

        return Task.FromResult(new ResultadoEmision(Respuesta, false));
    }

    public Task ValidarAsync(
        Guid contribuyenteId,
        string? rol,
        EmitirComprobanteRequest request,
        CancellationToken ct = default)
    {
        ValidarLlamado = true;
        UltimoContribuyenteId = contribuyenteId;
        UltimoRol = rol;
        UltimoRequest = request;

        if (ErrorValidar is not null)
        {
            throw ErrorValidar;
        }

        return Task.CompletedTask;
    }
}
