using FacturAR.Application.Abstractions;
using FacturAR.Application.Comprobantes;
using FacturAR.Contracts.Comprobantes;
using FacturAR.Domain.Entities;
using FacturAR.Domain.Enums;
using FacturAR.Domain.Exceptions;

namespace FacturAR.UnitTests;

public sealed class EmitirComprobanteServiceTests
{
    private static readonly DateTime FechaEmision = new(2026, 10, 28, 15, 30, 0, DateTimeKind.Utc);

    [Fact]
    public async Task CP0501_FacturaA_DevuelveImportesNumeroCaeYVencimiento()
    {
        var contexto = new ComprobanteContexto();
        contexto.Secuencias.Numero = 42;

        var resultado = await contexto.Servicio.EmitirAsync(
            contexto.Emisor.Id,
            "Emisor",
            contexto.FacturaA() with { FechaEmision = FechaEmision },
            "idem-cp0501");

        Assert.NotEqual(Guid.Empty, resultado.Comprobante.Id);
        Assert.Equal((int)TipoComprobante.FacturaA, resultado.Comprobante.TipoComprobante);
        Assert.Equal("Factura A", resultado.Comprobante.TipoComprobanteDescripcion);
        Assert.Equal(1, resultado.Comprobante.PuntoVenta);
        Assert.Equal(42, resultado.Comprobante.Numero);
        Assert.Equal(FechaEmision, resultado.Comprobante.FechaEmision);
        Assert.Matches("^[1-9][0-9]{13}$", resultado.Comprobante.Cae);
        Assert.Equal(FechaEmision.Date.AddDays(10), resultado.Comprobante.CaeVencimiento);
        Assert.Equal(10000m, resultado.Comprobante.ImporteNeto);
        Assert.Equal(2100m, resultado.Comprobante.ImporteIva);
        Assert.Equal(12100m, resultado.Comprobante.ImporteTotal);
        Assert.Equal("ARS", resultado.Comprobante.Moneda);
        Assert.Equal("Emitido", resultado.Comprobante.Estado);
        Assert.Equal("CUIT", resultado.Comprobante.Receptor.DocumentoTipo);
        Assert.Equal(ComprobanteContexto.CuitReceptor, resultado.Comprobante.Receptor.DocumentoNumero);
        Assert.Equal("Cliente S.A.", resultado.Comprobante.Receptor.RazonSocial);
        Assert.Equal((int)CondicionIva.ResponsableInscripto, resultado.Comprobante.Receptor.CondicionIva);
        Assert.Equal((contexto.Emisor.Id, 1, TipoComprobante.FacturaA), contexto.Secuencias.UltimaClave);
        Assert.Equal(1, contexto.Transaccion.Ejecuciones);
    }

    [Fact]
    public async Task CP0502_FacturaC_DevuelveImportesSinIva()
    {
        var contexto = new ComprobanteContexto();

        var resultado = await contexto.Servicio.EmitirAsync(
            contexto.Emisor.Id,
            "Emisor",
            contexto.FacturaC(),
            "idem-cp0502");

        Assert.Equal((int)TipoComprobante.FacturaC, resultado.Comprobante.TipoComprobante);
        Assert.Equal("Factura C", resultado.Comprobante.TipoComprobanteDescripcion);
        Assert.Equal(10000m, resultado.Comprobante.ImporteNeto);
        Assert.Equal(0m, resultado.Comprobante.ImporteIva);
        Assert.Equal(10000m, resultado.Comprobante.ImporteTotal);
        Assert.Equal("Emitido", resultado.Comprobante.Estado);
        Assert.Matches("^[1-9][0-9]{13}$", resultado.Comprobante.Cae);
    }

    [Fact]
    public async Task CP0503_FacturaB_NoDiscriminaElIvaIncluidoEnElPrecio()
    {
        var contexto = new ComprobanteContexto();

        var resultado = await contexto.Servicio.EmitirAsync(
            contexto.Emisor.Id,
            "Emisor",
            contexto.FacturaB(),
            "idem-cp0503");

        Assert.Equal((int)TipoComprobante.FacturaB, resultado.Comprobante.TipoComprobante);
        Assert.Equal("Factura B", resultado.Comprobante.TipoComprobanteDescripcion);
        Assert.Equal(12100m, resultado.Comprobante.ImporteNeto);
        Assert.Equal(0m, resultado.Comprobante.ImporteIva);
        Assert.Equal(12100m, resultado.Comprobante.ImporteTotal);
    }

    [Fact]
    public async Task CP0517_FacturaCConAlicuotaVeintiuno_NoAplicaElIva()
    {
        var contexto = new ComprobanteContexto();

        var resultado = await contexto.Servicio.EmitirAsync(
            contexto.Emisor.Id,
            "Emisor",
            contexto.FacturaC(alicuotaIva: 21m),
            "idem-cp0517");

        Assert.Equal(10000m, resultado.Comprobante.ImporteNeto);
        Assert.Equal(0m, resultado.Comprobante.ImporteIva);
        Assert.Equal(10000m, resultado.Comprobante.ImporteTotal);
    }

    [Fact]
    public async Task CP0520_FacturaAConReceptorResponsableInscripto_DiscriminaElIva()
    {
        var contexto = new ComprobanteContexto();

        var resultado = await contexto.Servicio.EmitirAsync(
            contexto.Emisor.Id,
            "Emisor",
            contexto.FacturaA(),
            "idem-cp0520");

        Assert.Equal(10000m, resultado.Comprobante.ImporteNeto);
        Assert.Equal(2100m, resultado.Comprobante.ImporteIva);
        Assert.Equal(12100m, resultado.Comprobante.ImporteTotal);
        Assert.Equal((int)CondicionIva.ResponsableInscripto, resultado.Comprobante.Receptor.CondicionIva);
    }

    [Fact]
    public async Task Emitir_ConDiasDeVencimientoConfigurados_UsaElParametro()
    {
        var contexto = new ComprobanteContexto();
        contexto.Parametros.DiasVencimiento = 30;

        var resultado = await contexto.Servicio.EmitirAsync(
            contexto.Emisor.Id,
            "Emisor",
            contexto.FacturaA() with { FechaEmision = FechaEmision },
            "idem-vencimiento");

        Assert.Equal(FechaEmision.Date.AddDays(30), resultado.Comprobante.CaeVencimiento);
    }

    [Fact]
    public async Task Emitir_ConMonedaConfigurada_UsaElParametro()
    {
        var contexto = new ComprobanteContexto();
        contexto.Parametros.Moneda = "USD";

        var resultado = await contexto.Servicio.EmitirAsync(
            contexto.Emisor.Id,
            "Emisor",
            contexto.FacturaC(),
            "idem-moneda");

        Assert.Equal("USD", resultado.Comprobante.Moneda);
    }

    [Fact]
    public async Task CP0506_ImporteTotalInconsistente_LanzaValidacionSinConsumirNumero()
    {
        var contexto = new ComprobanteContexto();

        var ex = await Assert.ThrowsAsync<ComprobanteValidationException>(
            () => contexto.Servicio.EmitirAsync(
                contexto.Emisor.Id,
                "Emisor",
                contexto.FacturaA(importeTotal: 9999m),
                "idem-cp0506"));

        Assert.Equal("El importe total no coincide con el cÃ¡lculo de los Ã­tems.", ex.Message);
        Assert.Equal(0, contexto.Secuencias.Llamadas);
        Assert.Equal(0, contexto.Transaccion.Ejecuciones);
        Assert.Empty(contexto.Comprobantes.Comprobantes);
        Assert.Empty(contexto.Eventos.Eventos);
    }

    [Fact]
    public async Task CP0507_PuntoDeVentaDeOtroContribuyente_LanzaNoEncontradoSinConsumirNumero()
    {
        var contexto = new ComprobanteContexto();

        var ex = await Assert.ThrowsAsync<ComprobanteNoEncontradoException>(
            () => contexto.Servicio.EmitirAsync(
                contexto.Emisor.Id,
                "Emisor",
                contexto.FacturaA() with { PuntoVenta = 2 },
                "idem-cp0507"));

        Assert.Equal(
            "El punto de venta no existe o no pertenece al contribuyente autenticado.",
            ex.Message);
        Assert.Equal(0, contexto.Secuencias.Llamadas);
        Assert.Empty(contexto.Comprobantes.Comprobantes);
    }

    [Fact]
    public async Task CP0508_PuntoDeVentaInactivo_LanzaPuntoVentaInactivoSinConsumirNumero()
    {
        var contexto = new ComprobanteContexto();
        contexto.PuntosVenta.Agregar(new PuntoVenta
        {
            ContribuyenteId = contexto.Emisor.Id,
            Numero = 3,
            Descripcion = "Inactivo",
            Activo = false
        });

        var ex = await Assert.ThrowsAsync<PuntoVentaInactivoException>(
            () => contexto.Servicio.EmitirAsync(
                contexto.Emisor.Id,
                "Emisor",
                contexto.FacturaA() with { PuntoVenta = 3 },
                "idem-cp0508"));

        Assert.Equal("El punto de venta estÃ¡ inactivo.", ex.Message);
        Assert.Equal(0, contexto.Secuencias.Llamadas);
        Assert.Empty(contexto.Comprobantes.Comprobantes);
    }

    [Fact]
    public async Task CP0510_RolConsulta_LanzaSinPermisosSinTocarLosRepositorios()
    {
        var contexto = new ComprobanteContexto();

        var ex = await Assert.ThrowsAsync<SinPermisosException>(
            () => contexto.Servicio.EmitirAsync(
                contexto.Emisor.Id,
                "Consulta",
                contexto.FacturaA(),
                "idem-cp0510"));

        Assert.Equal("El rol del principal no permite emitir comprobantes.", ex.Message);
        Assert.Equal(0, contexto.Contribuyentes.Consultas);
        Assert.Equal(0, contexto.PuntosVenta.Consultas);
        Assert.Equal(0, contexto.Secuencias.Llamadas);
        Assert.Equal(0, contexto.Transaccion.Ejecuciones);
        Assert.Empty(contexto.Comprobantes.Comprobantes);
    }

    [Fact]
    public async Task Emitir_ConRolNuloPorApiKey_EmiteElComprobante()
    {
        var contexto = new ComprobanteContexto();

        var resultado = await contexto.Servicio.EmitirAsync(
            contexto.Emisor.Id,
            null,
            contexto.FacturaC(),
            "idem-apikey");

        Assert.Equal("Emitido", resultado.Comprobante.Estado);
        Assert.Single(contexto.Comprobantes.Comprobantes);
    }

    [Theory]
    [InlineData("Administrador")]
    [InlineData("Emisor")]
    public async Task Emitir_ConRolesHabilitados_EmiteElComprobante(string rol)
    {
        var contexto = new ComprobanteContexto();

        var resultado = await contexto.Servicio.EmitirAsync(
            contexto.Emisor.Id,
            rol,
            contexto.FacturaC(),
            $"idem-{rol}");

        Assert.Equal("Emitido", resultado.Comprobante.Estado);
    }

    [Fact]
    public async Task CP0511_ContribuyenteInactivo_LanzaCuentaInactivaSinConsumirNumero()
    {
        var contexto = new ComprobanteContexto();
        contexto.Emisor.Activo = false;

        await Assert.ThrowsAsync<CuentaInactivaException>(
            () => contexto.Servicio.EmitirAsync(
                contexto.Emisor.Id,
                "Emisor",
                contexto.FacturaA(),
                "idem-cp0511"));

        Assert.Equal(0, contexto.Secuencias.Llamadas);
        Assert.Empty(contexto.Comprobantes.Comprobantes);
    }

    [Fact]
    public async Task CP0511_ContribuyenteInexistente_LanzaCuentaInactivaSinConsumirNumero()
    {
        var contexto = new ComprobanteContexto();

        await Assert.ThrowsAsync<CuentaInactivaException>(
            () => contexto.Servicio.EmitirAsync(
                Guid.NewGuid(),
                "Emisor",
                contexto.FacturaA(),
                "idem-inexistente"));

        Assert.Equal(0, contexto.Secuencias.Llamadas);
        Assert.Empty(contexto.Comprobantes.Comprobantes);
    }

    [Fact]
    public async Task CP0519_EmisorConsumidorFinalConFacturaA_LanzaEmisionInvalida()
    {
        var contexto = new ComprobanteContexto();
        contexto.Emisor.CondicionIva = CondicionIva.ConsumidorFinal;

        var ex = await Assert.ThrowsAsync<EmisionInvalidaException>(
            () => contexto.Servicio.EmitirAsync(
                contexto.Emisor.Id,
                "Emisor",
                contexto.FacturaA(),
                "idem-cp0519"));

        Assert.Equal("Un emisor consumidor final no puede emitir una factura A.", ex.Message);
        Assert.Equal(0, contexto.Secuencias.Llamadas);
        Assert.Empty(contexto.Comprobantes.Comprobantes);
    }

    [Fact]
    public async Task Emitir_PersisteElComprobanteConLosDatosDelContextoYElEventoDeAuditoria()
    {
        var contexto = new ComprobanteContexto();

        var resultado = await contexto.Servicio.EmitirAsync(
            contexto.Emisor.Id,
            "Administrador",
            contexto.FacturaA(),
            "idem-persistencia");

        var guardado = Assert.Single(contexto.Comprobantes.Comprobantes);
        Assert.Equal(resultado.Comprobante.Id, guardado.Id);
        Assert.Equal("idem-persistencia", guardado.IdempotencyKey);
        Assert.Equal(contexto.Emisor.Id, guardado.ContribuyenteId);
        Assert.Equal(contexto.Emisor.Cuit, guardado.EmisorCuit);
        Assert.Equal(contexto.Emisor.RazonSocial, guardado.EmisorRazonSocial);
        Assert.Equal(1, guardado.PuntoVenta);
        Assert.Equal(TipoComprobante.FacturaA, guardado.TipoComprobante);
        Assert.Equal(resultado.Comprobante.Numero, guardado.Numero);
        Assert.Equal(resultado.Comprobante.Cae, guardado.Cae);
        Assert.Equal(EstadoComprobante.Emitido, guardado.Estado);
        Assert.Equal(10000m, guardado.ImporteNeto);
        Assert.Equal(2100m, guardado.ImporteIva);
        Assert.Equal(12100m, guardado.ImporteTotal);
        Assert.Equal("ARS", guardado.Moneda);
        Assert.Equal(DateTimeKind.Utc, guardado.CreatedAt.Kind);
        Assert.Equal(DateTimeKind.Utc, guardado.FechaEmision.Kind);
        Assert.Equal(DateTimeKind.Utc, guardado.CaeVencimiento.Kind);

        var item = Assert.Single(guardado.Items);
        Assert.Equal("Servicio", item.Descripcion);
        Assert.Equal(2m, item.Cantidad);
        Assert.Equal(5000m, item.PrecioUnitario);
        Assert.Equal(21m, item.AlicuotaIva);
        Assert.Equal(10000m, item.Subtotal);

        var evento = Assert.Single(contexto.Eventos.Eventos);
        Assert.Equal("comprobante.emitido", evento.Accion);
        Assert.Equal("Comprobante", evento.Entidad);
        Assert.Equal(guardado.Id.ToString(), evento.EntidadId);
        Assert.Equal(contexto.Emisor.Id, evento.ContribuyenteId);
        Assert.Null(evento.UsuarioId);
        Assert.Equal("Empresa", evento.ActorTipo);
        Assert.Contains(resultado.Comprobante.Cae, evento.Datos ?? string.Empty, StringComparison.Ordinal);
        Assert.Contains("\"numero\":\"42\"", evento.Datos ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Emitir_ConItemsNulos_LanzaValidacionSinExcepcionDeReferenciaNula()
    {
        var contexto = new ComprobanteContexto();
        var request = contexto.FacturaA() with
        {
            Items = new List<ItemComprobanteRequest> { null! }
        };

        var ex = await Assert.ThrowsAsync<ComprobanteValidationException>(
            () => contexto.Servicio.EmitirAsync(contexto.Emisor.Id, "Emisor", request, "idem-items-nulos"));

        Assert.Equal("Los Ã­tems del comprobante no son vÃ¡lidos.", ex.Message);
        Assert.Equal(0, contexto.Secuencias.Llamadas);
        Assert.Empty(contexto.Comprobantes.Comprobantes);
    }

    [Fact]
    public async Task Emitir_SinItems_LanzaValidacionSinConsumirNumero()
    {
        var contexto = new ComprobanteContexto();
        var request = contexto.FacturaA() with { Items = null };

        var ex = await Assert.ThrowsAsync<ComprobanteValidationException>(
            () => contexto.Servicio.EmitirAsync(contexto.Emisor.Id, "Emisor", request, "idem-sin-items"));

        Assert.Equal("El comprobante debe tener entre 1 y 500 Ã­tems.", ex.Message);
        Assert.Equal(0, contexto.Secuencias.Llamadas);
    }

    [Fact]
    public async Task Emitir_ConFechaExtrema_LanzaValidacionSinConsumirNumero()
    {
        var contexto = new ComprobanteContexto();
        var request = contexto.FacturaA() with
        {
            FechaEmision = new DateTime(9999, 12, 31, 0, 0, 0, DateTimeKind.Utc)
        };

        var ex = await Assert.ThrowsAsync<ComprobanteValidationException>(
            () => contexto.Servicio.EmitirAsync(contexto.Emisor.Id, "Emisor", request, "idem-fecha-extrema"));

        Assert.Equal("La fecha de emisiÃ³n no es vÃ¡lida.", ex.Message);
        Assert.Equal(0, contexto.Secuencias.Llamadas);
        Assert.Empty(contexto.Comprobantes.Comprobantes);
    }

    [Fact]
    public async Task Emitir_ConAlicuotaNoPermitida_LanzaValidacionSinConsumirNumero()
    {
        var contexto = new ComprobanteContexto();

        var ex = await Assert.ThrowsAsync<ComprobanteValidationException>(
            () => contexto.Servicio.EmitirAsync(
                contexto.Emisor.Id,
                "Emisor",
                contexto.FacturaA(alicuotaIva: 12m),
                "idem-cp0516"));

        Assert.Equal("La alÃ­cuota de IVA no estÃ¡ permitida.", ex.Message);
        Assert.Equal(0, contexto.Secuencias.Llamadas);
    }

    [Fact]
    public void RequestDeEmision_NoExponeElContribuyenteNiElEmisor()
    {
        var propiedades = typeof(EmitirComprobanteRequest)
            .GetProperties()
            .Select(p => p.Name)
            .ToList();

        Assert.DoesNotContain("ContribuyenteId", propiedades);
        Assert.DoesNotContain("Contribuyente", propiedades);
        Assert.DoesNotContain("EmisorCuit", propiedades);
        Assert.DoesNotContain("Cae", propiedades);
        Assert.DoesNotContain("Numero", propiedades);
    }

    [Fact]
    public async Task Emitir_ReplayConMismaClaveYPayload_NoRenumeraNiDuplica()
    {
        var contexto = new ComprobanteContexto();
        var request = contexto.FacturaA() with { FechaEmision = FechaEmision };

        var primero = await contexto.Servicio.EmitirAsync(
            contexto.Emisor.Id,
            "Emisor",
            request,
            "pedido-001");
        var segundo = await contexto.Servicio.EmitirAsync(
            contexto.Emisor.Id,
            "Emisor",
            request,
            "pedido-001");

        Assert.False(primero.EsRepeticion);
        Assert.True(segundo.EsRepeticion);
        Assert.Equal(primero.Comprobante.Id, segundo.Comprobante.Id);
        Assert.Equal(primero.Comprobante.Numero, segundo.Comprobante.Numero);
        Assert.Equal(primero.Comprobante.Cae, segundo.Comprobante.Cae);
        Assert.Equal(1, contexto.Secuencias.Llamadas);
        Assert.Equal(1, contexto.Transaccion.Ejecuciones);
        Assert.Single(contexto.Comprobantes.Comprobantes);
        Assert.Single(contexto.Eventos.Eventos);
    }

    [Fact]
    public async Task Emitir_MismaClaveConPayloadDistinto_LanzaConflictoSinConsumirNumero()
    {
        var contexto = new ComprobanteContexto();
        var original = contexto.FacturaA() with { FechaEmision = FechaEmision };
        await contexto.Servicio.EmitirAsync(
            contexto.Emisor.Id,
            "Emisor",
            original,
            "pedido-001");

        var distinto = original with
        {
            ImporteTotal = 24200m,
            Items = [new ItemComprobanteRequest("Servicio", 2m, 10000m, 21m)]
        };

        await Assert.ThrowsAsync<IdempotenciaConflictoException>(
            () => contexto.Servicio.EmitirAsync(
                contexto.Emisor.Id,
                "Emisor",
                distinto,
                "pedido-001"));

        Assert.Equal(1, contexto.Secuencias.Llamadas);
        Assert.Equal(1, contexto.Transaccion.Ejecuciones);
        Assert.Single(contexto.Comprobantes.Comprobantes);
        Assert.Single(contexto.Eventos.Eventos);
    }

    [Fact]
    public async Task Emitir_CarreraConGanadorDisponible_DevuelveReplaySinDuplicar()
    {
        var contexto = new ComprobanteContexto();
        var request = contexto.FacturaA() with { FechaEmision = FechaEmision };
        var ganador = new Comprobante
        {
            ContribuyenteId = contexto.Emisor.Id,
            EmisorCuit = contexto.Emisor.Cuit,
            EmisorRazonSocial = contexto.Emisor.RazonSocial,
            PuntoVenta = 1,
            TipoComprobante = TipoComprobante.FacturaA,
            Numero = 7,
            FechaEmision = FechaEmision,
            Cae = "12345678901234",
            CaeVencimiento = FechaEmision.AddDays(10),
            ImporteNeto = 10000m,
            ImporteIva = 2100m,
            ImporteTotal = 12100m,
            IdempotencyKey = "pedido-001",
            IdempotencyPayloadHash = PayloadHasher.Hash(request)
        };
        var repositorio = new ComprobanteRepositorioCarreraFake(ganador);
        var servicio = contexto.CrearServicio(repositorio);

        var resultado = await servicio.EmitirAsync(
            contexto.Emisor.Id,
            "Emisor",
            request,
            "pedido-001");

        Assert.True(resultado.EsRepeticion);
        Assert.Equal(ganador.Id, resultado.Comprobante.Id);
        Assert.Equal(7, resultado.Comprobante.Numero);
        Assert.Equal("12345678901234", resultado.Comprobante.Cae);
        Assert.Equal(1, repositorio.Inserciones);
        Assert.Empty(contexto.Comprobantes.Comprobantes);
    }

    [Fact]
    public async Task Emitir_CarreraSinGanadorDisponible_LanzaConflicto()
    {
        var contexto = new ComprobanteContexto();
        var request = contexto.FacturaA() with { FechaEmision = FechaEmision };
        var repositorio = new ComprobanteRepositorioCarreraFake(null);
        var servicio = contexto.CrearServicio(repositorio);

        await Assert.ThrowsAsync<IdempotenciaConflictoException>(
            () => servicio.EmitirAsync(
                contexto.Emisor.Id,
                "Emisor",
                request,
                "pedido-001"));

        Assert.Equal(1, repositorio.Inserciones);
        Assert.Empty(contexto.Comprobantes.Comprobantes);
    }

    [Fact]
    public async Task Emitir_MismaClaveEnOtroContribuyente_EmiteUnComprobanteNuevo()
    {
        var contexto = new ComprobanteContexto();
        var otro = new Contribuyente
        {
            RazonSocial = "Otra SRL",
            Cuit = "20111111112",
            CondicionIva = CondicionIva.ResponsableInscripto,
            Activo = true
        };
        contexto.Contribuyentes.Agregar(otro);
        contexto.PuntosVenta.Agregar(new PuntoVenta
        {
            ContribuyenteId = otro.Id,
            Numero = 1,
            Descripcion = "Principal",
            Activo = true
        });
        var request = contexto.FacturaA() with { FechaEmision = FechaEmision };

        var primero = await contexto.Servicio.EmitirAsync(
            contexto.Emisor.Id,
            "Emisor",
            request,
            "pedido-001");
        var segundo = await contexto.Servicio.EmitirAsync(
            otro.Id,
            "Emisor",
            request,
            "pedido-001");

        Assert.False(primero.EsRepeticion);
        Assert.False(segundo.EsRepeticion);
        Assert.NotEqual(primero.Comprobante.Id, segundo.Comprobante.Id);
        Assert.Equal(2, contexto.Secuencias.Llamadas);
        Assert.Equal(2, contexto.Comprobantes.Comprobantes.Count);
        Assert.Equal(
            2,
            contexto.Comprobantes.Comprobantes
                .Select(c => c.ContribuyenteId)
                .Distinct()
                .Count());
    }

    [Fact]
    public async Task Emitir_PersisteLaClaveYElHashDelPayload()
    {
        var contexto = new ComprobanteContexto();
        var request = contexto.FacturaA() with { FechaEmision = FechaEmision };

        await contexto.Servicio.EmitirAsync(
            contexto.Emisor.Id,
            "Emisor",
            request,
            "pedido-001");

        var guardado = Assert.Single(contexto.Comprobantes.Comprobantes);
        Assert.Equal("pedido-001", guardado.IdempotencyKey);
        Assert.Equal(PayloadHasher.Hash(request), guardado.IdempotencyPayloadHash);
        Assert.Matches("^[0-9a-f]{64}$", guardado.IdempotencyPayloadHash);
    }

    private sealed class ComprobanteRepositorioCarreraFake(Comprobante? ganador) : IComprobanteRepository
    {
        private int _lecturas;

        public Comprobante? Ganador { get; } = ganador;

        public int Inserciones { get; private set; }

        public Task<Comprobante?> GetByCaeAsync(string cae, CancellationToken ct = default)
            => Task.FromResult<Comprobante?>(null);

        public Task<Comprobante?> GetByIdAsync(
            Guid contribuyenteId,
            Guid id,
            CancellationToken ct = default)
            => Task.FromResult<Comprobante?>(null);

        public Task<Comprobante?> GetByNumeracionAsync(
            Guid contribuyenteId,
            int puntoVenta,
            TipoComprobante tipo,
            long numero,
            CancellationToken ct = default)
            => Task.FromResult<Comprobante?>(null);

        public Task<Comprobante?> GetUltimoEmitidoAsync(
            Guid contribuyenteId,
            int puntoVenta,
            TipoComprobante tipo,
            CancellationToken ct = default)
            => Task.FromResult<Comprobante?>(null);

        public Task<(IReadOnlyList<Comprobante> Items, long Total)> ListarAsync(
            Guid contribuyenteId,
            FiltroListadoComprobantes filtro,
            int page,
            int pageSize,
            CancellationToken ct = default)
            => Task.FromResult<(IReadOnlyList<Comprobante>, long)>(([], 0));

        public Task<Comprobante?> GetByIdempotencyKeyAsync(
            Guid contribuyenteId,
            string clave,
            CancellationToken ct = default)
        {
            _lecturas++;
            return Task.FromResult(_lecturas == 1 ? null : Ganador);
        }

        public Task AddAsync(Comprobante comprobante, CancellationToken ct = default)
        {
            Inserciones++;
            throw new IdempotenciaDuplicadaException();
        }

        public Task<bool> AjustarSaldoAsync(
            Guid contribuyenteId,
            Guid originalId,
            decimal? saldoEsperado,
            decimal saldoNuevo,
            CancellationToken ct = default)
            => Task.FromResult(false);
    }
}
