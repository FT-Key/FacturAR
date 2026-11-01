using FacturAR.Application.Abstractions;
using FacturAR.Application.Parametros;
using FacturAR.Application.Seed;
using FacturAR.Domain.Entities;
using FacturAR.Domain.Enums;

namespace FacturAR.UnitTests;

public sealed class SeedServiceTests
{
    private const string PasswordDemo = "DemoSegura2026";

    [Fact]
    public async Task EjecutarAsync_Deshabilitado_NoEscribeNada()
    {
        var contexto = new SeedContexto(new SeedOptions { Enabled = false, Password = PasswordDemo });

        var resultado = await contexto.Servicio.EjecutarAsync(SeedEnvironment.Development);

        Assert.False(resultado.Ejecutado);
        Assert.Null(resultado.Error);
        Assert.Equal(0, contexto.Repositorio.TotalEscrituras);
        Assert.Equal(0, contexto.Hasher.Llamadas);
    }

    [Fact]
    public async Task EjecutarAsync_SinPassword_DevuelveErrorYNoEscribeNada()
    {
        var contexto = new SeedContexto(new SeedOptions { Enabled = true });

        var resultado = await contexto.Servicio.EjecutarAsync(SeedEnvironment.Development);

        Assert.False(resultado.Ejecutado);
        Assert.Contains("Seed__Password", resultado.Error, StringComparison.Ordinal);
        Assert.Equal(0, contexto.Repositorio.TotalEscrituras);
    }

    [Fact]
    public async Task EjecutarAsync_PasswordDebil_DevuelveErrorSinEcoDeLaPassword()
    {
        const string password = "corta";
        var contexto = new SeedContexto(new SeedOptions { Enabled = true, Password = password });

        var resultado = await contexto.Servicio.EjecutarAsync(SeedEnvironment.Development);

        Assert.False(resultado.Ejecutado);
        Assert.NotNull(resultado.Error);
        Assert.DoesNotContain(password, resultado.Error, StringComparison.Ordinal);
        Assert.Equal(0, contexto.Repositorio.TotalEscrituras);
    }

    [Fact]
    public async Task EjecutarAsync_CuitInvalido_DevuelveErrorYNoEscribeNada()
    {
        var contexto = new SeedContexto(
            new SeedOptions { Enabled = true, Password = PasswordDemo, Cuit = "20123456780" });

        var resultado = await contexto.Servicio.EjecutarAsync(SeedEnvironment.Development);

        Assert.False(resultado.Ejecutado);
        Assert.Contains("Seed__Cuit", resultado.Error, StringComparison.Ordinal);
        Assert.Equal(0, contexto.Repositorio.TotalEscrituras);
    }

    [Fact]
    public async Task EjecutarAsync_EmailSinArroba_DevuelveErrorClaroYNoEscribeNada()
    {
        var contexto = new SeedContexto(
            new SeedOptions { Enabled = true, Password = PasswordDemo, Email = "sin-arroba" });

        var resultado = await contexto.Servicio.EjecutarAsync(SeedEnvironment.Development);

        Assert.False(resultado.Ejecutado);
        Assert.Contains("Seed__Email", resultado.Error, StringComparison.Ordinal);
        Assert.Equal(0, contexto.Repositorio.TotalEscrituras);
    }

    [Fact]
    public async Task CP1806_SeedCreaContribuyenteUsuariosAdministradorPuntoVentaSecuenciasYParametros()
    {
        var contexto = new SeedContexto(new SeedOptions { Enabled = true, Password = PasswordDemo });

        var resultado = await contexto.Servicio.EjecutarAsync(SeedEnvironment.Production);

        Assert.True(resultado.Ejecutado);
        Assert.Null(resultado.Error);
        Assert.Null(resultado.ApiKeyDemoValor);

        var contribuyente = Assert.Single(contexto.Repositorio.Contribuyentes);
        Assert.Equal("20123456786", contribuyente.Cuit);
        Assert.Equal("Empresa Demo (simulada)", contribuyente.RazonSocial);
        Assert.Equal(CondicionIva.ResponsableInscripto, contribuyente.CondicionIva);
        Assert.Equal("admin@demo.facturar.local", contribuyente.Email);
        Assert.True(contribuyente.Activo);

        Assert.Equal(3, contexto.Repositorio.Usuarios.Count);
        AssertUsuario(contexto, "admin@demo.facturar.local", RolUsuario.Administrador, contribuyente.Id);
        AssertUsuario(contexto, "admin+emisor@demo.facturar.local", RolUsuario.Emisor, contribuyente.Id);
        AssertUsuario(contexto, "admin+consulta@demo.facturar.local", RolUsuario.Consulta, contribuyente.Id);
        Assert.All(contexto.Repositorio.Usuarios, u => Assert.NotEqual(PasswordDemo, u.PasswordHash));
        Assert.All(contexto.Repositorio.Usuarios, u => Assert.NotNull(u.EmailVerificadoAt));

        var administrador = Assert.Single(contexto.Repositorio.Administradores);
        Assert.Equal("admin+superadmin@demo.facturar.local", administrador.Email);
        Assert.Equal("Administrador Demo", administrador.Nombre);
        Assert.Equal(RolGlobal.SuperAdmin, administrador.Rol);
        Assert.True(administrador.Activo);
        Assert.NotEqual(PasswordDemo, administrador.PasswordHash);

        var puntoVenta = Assert.Single(contexto.Repositorio.PuntosVenta);
        Assert.Equal(1, puntoVenta.Numero);
        Assert.Equal("Casa Central", puntoVenta.Descripcion);
        Assert.True(puntoVenta.Activo);

        Assert.Equal(3, contexto.Repositorio.Secuencias.Count);
        Assert.All(contexto.Repositorio.Secuencias, s => Assert.Equal(0, s.UltimoNumero));
        Assert.Contains(contexto.Repositorio.Secuencias, s => s.TipoComprobante == TipoComprobante.FacturaA);
        Assert.Contains(contexto.Repositorio.Secuencias, s => s.TipoComprobante == TipoComprobante.FacturaB);
        Assert.Contains(contexto.Repositorio.Secuencias, s => s.TipoComprobante == TipoComprobante.FacturaC);

        AssertParametros(contexto);
    }

    [Fact]
    public async Task SegundaEjecucion_NoDuplicaDatosEnElServicio()
    {
        var contexto = new SeedContexto(new SeedOptions { Enabled = true, Password = PasswordDemo });

        await contexto.Servicio.EjecutarAsync(SeedEnvironment.Production);
        var resultado = await contexto.Servicio.EjecutarAsync(SeedEnvironment.Production);

        Assert.True(resultado.Ejecutado);
        Assert.Single(contexto.Repositorio.Contribuyentes);
        Assert.Equal(3, contexto.Repositorio.Usuarios.Count);
        Assert.Single(contexto.Repositorio.Administradores);
        Assert.Single(contexto.Repositorio.PuntosVenta);
        Assert.Equal(3, contexto.Repositorio.Secuencias.Count);
        Assert.Equal(ParametrosFacturacion.Claves.Count, contexto.Repositorio.Parametros.Count);
    }

    [Fact]
    public async Task SecuenciaExistente_NoSeSobrescribeEnElServicio()
    {
        var contexto = new SeedContexto(new SeedOptions { Enabled = true, Password = PasswordDemo });
        await contexto.Servicio.EjecutarAsync(SeedEnvironment.Production);
        var secuencia = contexto.Repositorio.Secuencias.Single(
            s => s.TipoComprobante == TipoComprobante.FacturaA);
        secuencia.UltimoNumero = 7;

        await contexto.Servicio.EjecutarAsync(SeedEnvironment.Production);

        Assert.Equal(7, contexto.Repositorio.Secuencias.Single(
            s => s.TipoComprobante == TipoComprobante.FacturaA).UltimoNumero);
    }

    [Fact]
    public async Task CP1812_DevelopmentSinApiKeyDemo_NoCreaApiKey()
    {
        var contexto = new SeedContexto(
            new SeedOptions { Enabled = true, Password = PasswordDemo, ApiKeyDemo = false });

        var resultado = await contexto.Servicio.EjecutarAsync(SeedEnvironment.Development);

        Assert.True(resultado.Ejecutado);
        Assert.Empty(contexto.Repositorio.ApiKeys);
        Assert.Null(resultado.ApiKeyDemoValor);
    }

    [Fact]
    public async Task CP1813_DevelopmentConApiKeyDemo_CreaLaKeyYDevuelveElValorUnaVez()
    {
        var contexto = new SeedContexto(
            new SeedOptions { Enabled = true, Password = PasswordDemo, ApiKeyDemo = true });

        var primera = await contexto.Servicio.EjecutarAsync(SeedEnvironment.Development);
        var segunda = await contexto.Servicio.EjecutarAsync(SeedEnvironment.Development);

        Assert.True(primera.ApiKeyDemoCreada);
        Assert.NotNull(primera.ApiKeyDemoValor);
        Assert.False(segunda.ApiKeyDemoCreada);
        Assert.Null(segunda.ApiKeyDemoValor);
        var apiKey = Assert.Single(contexto.Repositorio.ApiKeys);
        Assert.Equal(SeedService.NombreApiKeyDemo, apiKey.Nombre);
        Assert.NotEqual(primera.ApiKeyDemoValor, apiKey.Hash);
        Assert.Equal(contexto.ApiKeyHasher.Hash(primera.ApiKeyDemoValor!), apiKey.Hash);
    }

    [Fact]
    public async Task ProductionConApiKeyDemoActiva_NoCreaLaKey()
    {
        var contexto = new SeedContexto(
            new SeedOptions { Enabled = true, Password = PasswordDemo, ApiKeyDemo = true });

        var resultado = await contexto.Servicio.EjecutarAsync(SeedEnvironment.Production);

        Assert.True(resultado.Ejecutado);
        Assert.Empty(contexto.Repositorio.ApiKeys);
        Assert.Null(resultado.ApiKeyDemoValor);
    }

    [Fact]
    public async Task CP1810_ProductionConSeedDeshabilitado_NoEscribeNada()
    {
        var contexto = new SeedContexto(new SeedOptions { Enabled = false, Password = PasswordDemo });

        var resultado = await contexto.Servicio.EjecutarAsync(SeedEnvironment.Production);

        Assert.False(resultado.Ejecutado);
        Assert.Null(resultado.Error);
        Assert.Equal(0, contexto.Repositorio.TotalEscrituras);
        Assert.Equal(0, contexto.Hasher.Llamadas);
    }

    [Fact]
    public async Task EmailDemo_NormalizaYDerivaLosRoles()
    {
        var contexto = new SeedContexto(new SeedOptions
        {
            Enabled = true,
            Password = PasswordDemo,
            Email = " Duenio@Empresa.COM "
        });

        await contexto.Servicio.EjecutarAsync(SeedEnvironment.Production);

        var contribuyente = Assert.Single(contexto.Repositorio.Contribuyentes);
        Assert.Equal("duenio@empresa.com", contribuyente.Email);
        Assert.Contains(
            contexto.Repositorio.Usuarios,
            u => u.Email == "duenio+emisor@empresa.com" && u.Rol == RolUsuario.Emisor);
        Assert.Contains(
            contexto.Repositorio.Usuarios,
            u => u.Email == "duenio+consulta@empresa.com" && u.Rol == RolUsuario.Consulta);
        Assert.Equal(
            "duenio+superadmin@empresa.com",
            Assert.Single(contexto.Repositorio.Administradores).Email);
    }

    [Fact]
    public async Task PuntoVentaPorDefectoDeConfiguracion_SeAplicaAlPuntoDeVentaYLasSecuencias()
    {
        var contexto = new SeedContexto(
            new SeedOptions { Enabled = true, Password = PasswordDemo },
            new ParametrosFacturacion { PuntoVentaPorDefecto = 7 });

        await contexto.Servicio.EjecutarAsync(SeedEnvironment.Production);

        Assert.Equal(7, Assert.Single(contexto.Repositorio.PuntosVenta).Numero);
        Assert.Equal(3, contexto.Repositorio.Secuencias.Count);
        Assert.All(contexto.Repositorio.Secuencias, s => Assert.Equal(7, s.PuntoVenta));
    }

    [Fact]
    public async Task ParametrosSePersistenConUpdatedAtUtc()
    {
        var contexto = new SeedContexto(new SeedOptions { Enabled = true, Password = PasswordDemo });

        var antes = DateTime.UtcNow;
        await contexto.Servicio.EjecutarAsync(SeedEnvironment.Production);

        Assert.All(contexto.Repositorio.Parametros, p =>
        {
            Assert.Equal(DateTimeKind.Utc, p.UpdatedAt.Kind);
            Assert.InRange(p.UpdatedAt, antes.AddSeconds(-1), DateTime.UtcNow.AddSeconds(1));
        });
    }

    [Fact]
    public async Task ParametrosSePersistenGlobalesConClaveYValor()
    {
        var contexto = new SeedContexto(new SeedOptions { Enabled = true, Password = PasswordDemo });

        await contexto.Servicio.EjecutarAsync(SeedEnvironment.Production);

        Assert.All(contexto.Repositorio.Parametros, p => Assert.Null(p.ContribuyenteId));
        Assert.Equal("ARS", contexto.Repositorio.Parametros.Single(p => p.Clave == "Moneda").Valor);
    }

    private static void AssertUsuario(
        SeedContexto contexto,
        string email,
        RolUsuario rol,
        Guid contribuyenteId)
    {
        var usuario = contexto.Repositorio.Usuarios.Single(u => u.Email == email);
        Assert.Equal(rol, usuario.Rol);
        Assert.Equal(contribuyenteId, usuario.ContribuyenteId);
        Assert.True(usuario.Activo);
        Assert.Equal(contexto.Hasher.Hash(PasswordDemo), usuario.PasswordHash);
    }

    private static void AssertParametros(SeedContexto contexto)
    {
        Assert.Equal(ParametrosFacturacion.Claves.Count, contexto.Repositorio.Parametros.Count);
        Assert.Equal("10", contexto.Repositorio.Parametros.Single(p => p.Clave == "DiasVencimientoCae").Valor);
        Assert.Equal("0;5;10.5;21;27", contexto.Repositorio.Parametros.Single(p => p.Clave == "AlicuotasPermitidas").Valor);
        Assert.Equal("1", contexto.Repositorio.Parametros.Single(p => p.Clave == "PuntoVentaPorDefecto").Valor);
        Assert.Equal("ARS", contexto.Repositorio.Parametros.Single(p => p.Clave == "Moneda").Valor);
        Assert.Equal("20", contexto.Repositorio.Parametros.Single(p => p.Clave == "PaginaPorDefecto").Valor);
        Assert.Equal("100", contexto.Repositorio.Parametros.Single(p => p.Clave == "PaginaMaxima").Valor);
    }

    private sealed class SeedContexto
    {
        public SeedContexto(SeedOptions options, ParametrosFacturacion? parametros = null)
        {
            Repositorio = new SeedRepositoryFake();
            Hasher = new HashingFake();
            ApiKeyHasher = new ApiKeyHasherFake();
            Servicio = new SeedService(
                options,
                parametros ?? new ParametrosFacturacion(),
                Repositorio,
                Hasher,
                new ApiKeyGeneratorFake(),
                ApiKeyHasher);
        }

        public SeedRepositoryFake Repositorio { get; }

        public HashingFake Hasher { get; }

        public ApiKeyHasherFake ApiKeyHasher { get; }

        public SeedService Servicio { get; }
    }

    private sealed class HashingFake : IPasswordHasher
    {
        public int Llamadas { get; private set; }

        public string Hash(string password)
        {
            Llamadas++;
            return $"pbkdf2-fake:{password}";
        }

        public bool Verify(string password, string hash) => hash == $"pbkdf2-fake:{password}";

        public bool NeedsRehash(string hash) => false;
    }

    private sealed class ApiKeyGeneratorFake : IApiKeyGenerator
    {
        public ApiKeyGeneratorValue Generar()
            => new("0123456789ab", new string('a', 43), $"fak_0123456789ab_{new string('a', 43)}");
    }

    private sealed class ApiKeyHasherFake : IApiKeyHasher
    {
        public string Hash(string valor) => $"hash:{valor}";

        public bool Matches(string valor, string hash) => hash == Hash(valor);
    }

    private sealed class SeedRepositoryFake : ISeedRepository
    {
        public List<Contribuyente> Contribuyentes { get; } = [];

        public List<Usuario> Usuarios { get; } = [];

        public List<AdministradorFacturar> Administradores { get; } = [];

        public List<PuntoVenta> PuntosVenta { get; } = [];

        public List<Secuencia> Secuencias { get; } = [];

        public List<Parametro> Parametros { get; } = [];

        public List<ApiKey> ApiKeys { get; } = [];

        public int TotalEscrituras { get; private set; }

        public Task<Contribuyente> UpsertContribuyenteAsync(
            Contribuyente contribuyente,
            CancellationToken ct = default)
        {
            TotalEscrituras++;
            var existente = Contribuyentes.FirstOrDefault(c => c.Cuit == contribuyente.Cuit);

            if (existente is null)
            {
                Contribuyentes.Add(contribuyente);
                return Task.FromResult(contribuyente);
            }

            existente.RazonSocial = contribuyente.RazonSocial;
            existente.CondicionIva = contribuyente.CondicionIva;
            existente.Email = contribuyente.Email;
            existente.Activo = contribuyente.Activo;
            return Task.FromResult(existente);
        }

        public Task UpsertUsuarioAsync(Usuario usuario, CancellationToken ct = default)
        {
            TotalEscrituras++;
            var existente = Usuarios.FirstOrDefault(
                u => u.ContribuyenteId == usuario.ContribuyenteId && u.Email == usuario.Email);

            if (existente is null)
            {
                Usuarios.Add(usuario);
            }
            else
            {
                existente.Rol = usuario.Rol;
                existente.Activo = usuario.Activo;
            }

            return Task.CompletedTask;
        }

        public Task UpsertAdministradorAsync(
            AdministradorFacturar administrador,
            CancellationToken ct = default)
        {
            TotalEscrituras++;
            var existente = Administradores.FirstOrDefault(a => a.Email == administrador.Email);

            if (existente is null)
            {
                Administradores.Add(administrador);
            }
            else
            {
                existente.Nombre = administrador.Nombre;
                existente.Rol = administrador.Rol;
                existente.Activo = administrador.Activo;
            }

            return Task.CompletedTask;
        }

        public Task UpsertPuntoVentaAsync(PuntoVenta puntoVenta, CancellationToken ct = default)
        {
            TotalEscrituras++;
            var existente = PuntosVenta.FirstOrDefault(
                p => p.ContribuyenteId == puntoVenta.ContribuyenteId && p.Numero == puntoVenta.Numero);

            if (existente is null)
            {
                PuntosVenta.Add(puntoVenta);
            }
            else
            {
                existente.Descripcion = puntoVenta.Descripcion;
                existente.Activo = puntoVenta.Activo;
            }

            return Task.CompletedTask;
        }

        public Task UpsertSecuenciaAsync(Secuencia secuencia, CancellationToken ct = default)
        {
            TotalEscrituras++;
            var existente = Secuencias.FirstOrDefault(
                s => s.ContribuyenteId == secuencia.ContribuyenteId
                    && s.PuntoVenta == secuencia.PuntoVenta
                    && s.TipoComprobante == secuencia.TipoComprobante);

            if (existente is null)
            {
                Secuencias.Add(secuencia);
            }

            return Task.CompletedTask;
        }

        public Task UpsertParametroAsync(Parametro parametro, CancellationToken ct = default)
        {
            TotalEscrituras++;
            var existente = Parametros.FirstOrDefault(
                p => p.ContribuyenteId == parametro.ContribuyenteId && p.Clave == parametro.Clave);

            if (existente is null)
            {
                Parametros.Add(parametro);
            }
            else
            {
                existente.Valor = parametro.Valor;
                existente.UpdatedAt = parametro.UpdatedAt;
            }

            return Task.CompletedTask;
        }

        public Task<bool> CrearApiKeySiNoExisteAsync(ApiKey apiKey, CancellationToken ct = default)
        {
            TotalEscrituras++;
            var existente = ApiKeys.Any(
                a => a.ContribuyenteId == apiKey.ContribuyenteId && a.Nombre == apiKey.Nombre);

            if (existente)
            {
                return Task.FromResult(false);
            }

            ApiKeys.Add(apiKey);
            return Task.FromResult(true);
        }
    }
}
