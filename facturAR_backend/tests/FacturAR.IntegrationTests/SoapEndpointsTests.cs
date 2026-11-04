using System.Globalization;
using System.Net;
using System.Net.Http.Json;
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
        Assert.Contains("FECompUltimoAutorizado", xml);
        Assert.Contains("FECompConsultar", xml);
        Assert.Contains("FEDummy", xml);
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
    public async Task CP1602_FECompUltimoAutorizado_ConComprobantesEmitidos_DevuelveUltimoNroYCae()
    {
        using var factory = new SoapApiFactory();
        factory.Secuencias.UltimoNumero = 42;
        factory.Comprobantes.Comprobantes.Add(NuevoComprobante(SoapApiFactory.ContribuyenteId, numero: 42));
        using var client = factory.CreateClient();

        using var response = await EnviarConsultaAsync(
            client,
            "FECompUltimoAutorizado",
            """
            <PtoVta>1</PtoVta>
            <TipoComprobante>11</TipoComprobante>
            """,
            SoapApiFactory.ApiKeyValida);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var documento = XDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("A", Valor(documento, "Resultado"));
        Assert.Equal("1", Valor(documento, "PtoVta"));
        Assert.Equal("11", Valor(documento, "TipoComprobante"));
        Assert.Equal("42", Valor(documento, "UltimoNro"));
        Assert.Equal("12345678901234", Valor(documento, "Cae"));
        Assert.Equal("20261103", Valor(documento, "FechaEmision"));
        Assert.Equal("20261113", Valor(documento, "FchVto"));
        Assert.Null(Valor(documento, "Observaciones"));
    }

    [Fact]
    public async Task CP1603_FECompUltimoAutorizado_SinComprobantesEmitidos_DevuelveUltimoNroCeroSinCae()
    {
        using var factory = new SoapApiFactory();
        factory.Secuencias.UltimoNumero = 0;
        using var client = factory.CreateClient();

        using var response = await EnviarConsultaAsync(
            client,
            "FECompUltimoAutorizado",
            """
            <PtoVta>1</PtoVta>
            <TipoComprobante>11</TipoComprobante>
            """,
            SoapApiFactory.ApiKeyValida);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var documento = XDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("A", Valor(documento, "Resultado"));
        Assert.Equal("1", Valor(documento, "PtoVta"));
        Assert.Equal("11", Valor(documento, "TipoComprobante"));
        Assert.Equal("0", Valor(documento, "UltimoNro"));
        Assert.Null(Valor(documento, "Cae"));
        Assert.Null(Valor(documento, "FechaEmision"));
        Assert.Null(Valor(documento, "FchVto"));
    }

    [Fact]
    public async Task CP1604_FECompConsultar_ComprobantePropio_DevuelveDatosCompletos()
    {
        using var factory = new SoapApiFactory();
        var comprobante = NuevoComprobante(SoapApiFactory.ContribuyenteId, numero: 42);
        factory.Comprobantes.Comprobantes.Add(comprobante);
        using var client = factory.CreateClient();

        using var response = await EnviarConsultaAsync(
            client,
            "FECompConsultar",
            """
            <PtoVta>1</PtoVta>
            <TipoComprobante>11</TipoComprobante>
            <Numero>42</Numero>
            """,
            SoapApiFactory.ApiKeyValida);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var documento = XDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("A", Valor(documento, "Resultado"));
        Assert.Equal(comprobante.Id.ToString(), Valor(documento, "Id"));
        Assert.Equal("11", Valor(documento, "TipoComprobante"));
        Assert.Equal("Factura C", Valor(documento, "TipoComprobanteDescripcion"));
        Assert.Equal("1", Valor(documento, "PtoVta"));
        Assert.Equal("42", Valor(documento, "Numero"));
        Assert.Equal("20261103", Valor(documento, "FechaEmision"));
        Assert.Equal("12345678901234", Valor(documento, "Cae"));
        Assert.Equal("20261113", Valor(documento, "FchVto"));
        Assert.Equal("CUIT", Valor(documento, "DocTipo"));
        Assert.Equal("20345678901", Valor(documento, "DocNro"));
        Assert.Equal("Cliente Final", Valor(documento, "ReceptorRazonSocial"));
        Assert.Equal("5", Valor(documento, "CondicionIVA"));
        Assert.Equal("100", Valor(documento, "ImporteNeto"));
        Assert.Equal("0", Valor(documento, "ImporteIVA"));
        Assert.Equal("100", Valor(documento, "ImporteTotal"));
        Assert.Equal("100", Valor(documento, "SaldoPendiente"));
        Assert.Equal("ARS", Valor(documento, "Moneda"));
        Assert.Equal("Emitido", Valor(documento, "Estado"));
        Assert.Equal("Servicio", Valor(documento, "Descripcion"));
        Assert.Equal("100", Valor(documento, "Subtotal"));
    }

    [Fact]
    public async Task CP1605_FECompConsultar_ComprobanteDeOtraEmpresa_DevuelveComprobanteNoEncontradoSinFiltrarDatos()
    {
        using var factory = new SoapApiFactory();
        var comprobanteAjeno = NuevoComprobante(Guid.NewGuid(), numero: 42);
        factory.Comprobantes.Comprobantes.Add(comprobanteAjeno);
        using var client = factory.CreateClient();

        using var response = await EnviarConsultaAsync(
            client,
            "FECompConsultar",
            """
            <PtoVta>1</PtoVta>
            <TipoComprobante>11</TipoComprobante>
            <Numero>42</Numero>
            """,
            SoapApiFactory.ApiKeyValida);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var xml = await response.Content.ReadAsStringAsync();
        var documento = XDocument.Parse(xml);
        Assert.Equal("R", Valor(documento, "Resultado"));
        Assert.Contains("COMPROBANTE_NO_ENCONTRADO", Valor(documento, "Observaciones"));
        Assert.Null(Valor(documento, "Cae"));
        Assert.Null(Valor(documento, "ImporteTotal"));
        Assert.Null(Valor(documento, "ReceptorRazonSocial"));
        Assert.DoesNotContain(comprobanteAjeno.Cae, xml, StringComparison.Ordinal);
        Assert.DoesNotContain(comprobanteAjeno.ReceptorRazonSocial, xml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CP1606_FEDummy_ConApiKeyValida_DevuelveEstadoOkYMetadatos()
    {
        using var factory = new SoapApiFactory();
        using var client = factory.CreateClient();

        var antes = DateTime.UtcNow.AddSeconds(-5);
        using var response = await EnviarConsultaAsync(
            client,
            "FEDummy",
            string.Empty,
            SoapApiFactory.ApiKeyValida);
        var despues = DateTime.UtcNow.AddSeconds(5);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var documento = XDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("OK", Valor(documento, "Estado"));
        Assert.Equal("FacturAR.Api", Valor(documento, "Service"));
        Assert.False(string.IsNullOrWhiteSpace(Valor(documento, "Version")));
        var serverTimeTexto = Valor(documento, "ServerTime");
        Assert.NotNull(serverTimeTexto);
        Assert.EndsWith("Z", serverTimeTexto, StringComparison.Ordinal);

        var serverTime = DateTime.Parse(
            serverTimeTexto!,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
        Assert.InRange(serverTime, antes, despues);
    }

    [Fact]
    public async Task CP1607_FEDummy_SinApiKey_Devuelve401ApiKeyAusente()
    {
        using var factory = new SoapApiFactory();
        using var client = factory.CreateClient();

        using var response = await EnviarConsultaAsync(
            client,
            "FEDummy",
            string.Empty,
            apiKey: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var documento = XDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("API_KEY_AUSENTE", Valor(documento, "codigo"));
        Assert.Contains("Client", Valor(documento, "faultcode"));
    }

    [Fact]
    public async Task CP1608_FECompConsultar_ConApiKeyInvalida_Devuelve401ApiKeyInvalida()
    {
        using var factory = new SoapApiFactory();
        using var client = factory.CreateClient();

        using var response = await EnviarConsultaAsync(
            client,
            "FECompConsultar",
            """
            <PtoVta>1</PtoVta>
            <TipoComprobante>11</TipoComprobante>
            <Numero>42</Numero>
            """,
            SoapApiFactory.ApiKeyDesconocida);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var documento = XDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("API_KEY_INVALIDA", Valor(documento, "codigo"));
        Assert.Contains("Client", Valor(documento, "faultcode"));
        Assert.DoesNotContain(SoapApiFactory.ApiKeyDesconocida, documento.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(999, 1)]
    [InlineData(11, 0)]
    public async Task CP1609_FECompConsultar_ConParametrosInvalidos_DevuelveRechazoValidacion(
        int tipoComprobante,
        int puntoVenta)
    {
        using var factory = new SoapApiFactory();
        using var client = factory.CreateClient();

        using var response = await EnviarConsultaAsync(
            client,
            "FECompConsultar",
            $"""
            <PtoVta>{puntoVenta}</PtoVta>
            <TipoComprobante>{tipoComprobante}</TipoComprobante>
            <Numero>1</Numero>
            """,
            SoapApiFactory.ApiKeyValida);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var documento = XDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("R", Valor(documento, "Resultado"));
        Assert.StartsWith("VALIDACION: ", Valor(documento, "Observaciones"), StringComparison.Ordinal);
        Assert.Equal(0, factory.Comprobantes.ConsultasDetalle);
    }

    [Fact]
    public async Task CP1610_ConsistenciaRestVsSoap_EnConsultaDeComprobante()
    {
        using var factory = new SoapApiFactory();
        var comprobante = NuevoComprobante(SoapApiFactory.ContribuyenteId, numero: 42);
        factory.Comprobantes.Comprobantes.Add(comprobante);
        using var client = factory.CreateClient();

        using var restRequest = new HttpRequestMessage(HttpMethod.Get, $"/v1/comprobantes/{comprobante.Id}");
        restRequest.Headers.TryAddWithoutValidation("X-Api-Key", SoapApiFactory.ApiKeyValida);
        using var restResponse = await client.SendAsync(restRequest);
        Assert.Equal(HttpStatusCode.OK, restResponse.StatusCode);
        var detalleRest = await restResponse.Content.ReadFromJsonAsync<ComprobanteDetalleResponse>();
        Assert.NotNull(detalleRest);

        using var soapResponse = await EnviarConsultaAsync(
            client,
            "FECompConsultar",
            """
            <PtoVta>1</PtoVta>
            <TipoComprobante>11</TipoComprobante>
            <Numero>42</Numero>
            """,
            SoapApiFactory.ApiKeyValida);
        Assert.Equal(HttpStatusCode.OK, soapResponse.StatusCode);
        var soapDoc = XDocument.Parse(await soapResponse.Content.ReadAsStringAsync());

        Assert.Equal(detalleRest!.Id.ToString(), Valor(soapDoc, "Id"));
        Assert.Equal(detalleRest.Cae, Valor(soapDoc, "Cae"));
        Assert.Equal(detalleRest.Numero.ToString(CultureInfo.InvariantCulture), Valor(soapDoc, "Numero"));
        Assert.Equal(detalleRest.PuntoVenta.ToString(CultureInfo.InvariantCulture), Valor(soapDoc, "PtoVta"));
        Assert.Equal(detalleRest.TipoComprobante.ToString(CultureInfo.InvariantCulture), Valor(soapDoc, "TipoComprobante"));
        Assert.Equal(
            detalleRest.FechaEmision.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
            Valor(soapDoc, "FechaEmision"));
        Assert.Equal(
            detalleRest.FechaVencimientoCae.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
            Valor(soapDoc, "FchVto"));
        Assert.Equal(
            detalleRest.ImporteTotal.ToString(CultureInfo.InvariantCulture),
            Valor(soapDoc, "ImporteTotal"));
        Assert.Equal(detalleRest.Estado, Valor(soapDoc, "Estado"));
    }

    [Fact]
    public async Task CP1611_ConsistenciaRestVsSoap_EnUltimoAutorizado()
    {
        using var factory = new SoapApiFactory();
        factory.Secuencias.UltimoNumero = 42;
        factory.Comprobantes.Comprobantes.Add(NuevoComprobante(SoapApiFactory.ContribuyenteId, numero: 42));
        using var client = factory.CreateClient();

        using var restRequest = new HttpRequestMessage(
            HttpMethod.Get,
            "/v1/comprobantes/ultimo-autorizado?tipo=11&puntoVenta=1");
        restRequest.Headers.TryAddWithoutValidation("X-Api-Key", SoapApiFactory.ApiKeyValida);
        using var restResponse = await client.SendAsync(restRequest);
        Assert.Equal(HttpStatusCode.OK, restResponse.StatusCode);
        var ultimoRest = await restResponse.Content.ReadFromJsonAsync<UltimoAutorizadoResponse>();
        Assert.NotNull(ultimoRest);

        using var soapResponse = await EnviarConsultaAsync(
            client,
            "FECompUltimoAutorizado",
            """
            <PtoVta>1</PtoVta>
            <TipoComprobante>11</TipoComprobante>
            """,
            SoapApiFactory.ApiKeyValida);
        Assert.Equal(HttpStatusCode.OK, soapResponse.StatusCode);
        var soapDoc = XDocument.Parse(await soapResponse.Content.ReadAsStringAsync());

        Assert.Equal(
            ultimoRest!.UltimoNumero.ToString(CultureInfo.InvariantCulture),
            Valor(soapDoc, "UltimoNro"));
        Assert.Equal(
            ultimoRest.PuntoVenta.ToString(CultureInfo.InvariantCulture),
            Valor(soapDoc, "PtoVta"));
        Assert.Equal(
            ultimoRest.TipoComprobante.ToString(CultureInfo.InvariantCulture),
            Valor(soapDoc, "TipoComprobante"));
    }

    [Fact]
    public async Task CP1612_OperacionesDeConsulta_NoEscribenEnBaseDeDatos()
    {
        using var factory = new SoapApiFactory();
        factory.Secuencias.UltimoNumero = 42;
        factory.Comprobantes.Comprobantes.Add(NuevoComprobante(SoapApiFactory.ContribuyenteId, numero: 42));
        using var client = factory.CreateClient();

        var cantidadComprobantesInicial = factory.Comprobantes.Comprobantes.Count;
        var ultimoNumeroInicial = factory.Secuencias.UltimoNumero;
        var cantidadEventosInicial = factory.Eventos.Eventos.Count;

        using var respUltimo = await EnviarConsultaAsync(
            client,
            "FECompUltimoAutorizado",
            """
            <PtoVta>1</PtoVta>
            <TipoComprobante>11</TipoComprobante>
            """,
            SoapApiFactory.ApiKeyValida);
        Assert.Equal(HttpStatusCode.OK, respUltimo.StatusCode);

        using var respConsultar = await EnviarConsultaAsync(
            client,
            "FECompConsultar",
            """
            <PtoVta>1</PtoVta>
            <TipoComprobante>11</TipoComprobante>
            <Numero>42</Numero>
            """,
            SoapApiFactory.ApiKeyValida);
        Assert.Equal(HttpStatusCode.OK, respConsultar.StatusCode);

        using var respDummy = await EnviarConsultaAsync(
            client,
            "FEDummy",
            string.Empty,
            SoapApiFactory.ApiKeyValida);
        Assert.Equal(HttpStatusCode.OK, respDummy.StatusCode);

        Assert.Equal(cantidadComprobantesInicial, factory.Comprobantes.Comprobantes.Count);
        Assert.Equal(ultimoNumeroInicial, factory.Secuencias.UltimoNumero);
        Assert.Equal(cantidadEventosInicial, factory.Eventos.Eventos.Count);
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

    private static Comprobante NuevoComprobante(Guid contribuyenteId, long numero = 42)
        => new()
        {
            Id = Guid.NewGuid(),
            ContribuyenteId = contribuyenteId,
            PuntoVenta = 1,
            TipoComprobante = TipoComprobante.FacturaC,
            Numero = numero,
            FechaEmision = new DateTime(2026, 11, 3, 0, 0, 0, DateTimeKind.Utc),
            Cae = "12345678901234",
            FechaVencimientoCae = new DateTime(2026, 11, 13, 0, 0, 0, DateTimeKind.Utc),
            ReceptorDocumentoTipo = "CUIT",
            ReceptorDocumentoNumero = "20345678901",
            ReceptorRazonSocial = "Cliente Final",
            ReceptorCondicionIva = CondicionIva.ConsumidorFinal,
            Items =
            [
                new ComprobanteItem
                {
                    Descripcion = "Servicio",
                    Cantidad = 1m,
                    PrecioUnitario = 100m,
                    AlicuotaIva = 0m,
                    Subtotal = 100m
                }
            ],
            ImporteNeto = 100m,
            ImporteIva = 0m,
            ImporteTotal = 100m,
            SaldoPendiente = 100m,
            Moneda = "ARS",
            Estado = EstadoComprobante.Emitido
        };

    private static async Task<HttpResponseMessage> EnviarConsultaAsync(
        HttpClient client,
        string operacion,
        string cuerpoRequestXml,
        string? apiKey)
    {
        var xml = $"""
            <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
              <soap:Body>
                <{operacion} xmlns="http://ar.gov.afip.dif.FEV1Service/">
                  <request>
                    {cuerpoRequestXml}
                  </request>
                </{operacion}>
              </soap:Body>
            </soap:Envelope>
            """;

        return await EnviarAsync(
            client,
            operacion,
            CuitEmisor,
            apiKey,
            contenido: new StringContent(xml, Encoding.UTF8, "text/xml"));
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

    public ConsultaComprobantesRepositorioFake Comprobantes { get; } = new();

    public ConsultaSecuenciasRepositorioFake Secuencias { get; } = new();

    public ComprobanteEventoFake Eventos { get; } = new();

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

            services.RemoveAll<IComprobanteRepository>();
            services.AddSingleton<IComprobanteRepository>(Comprobantes);

            services.RemoveAll<ISecuenciaRepository>();
            services.AddSingleton<ISecuenciaRepository>(Secuencias);

            services.RemoveAll<IEventoAuditoriaRepository>();
            services.AddSingleton<IEventoAuditoriaRepository>(Eventos);

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
