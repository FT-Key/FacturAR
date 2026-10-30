using FacturAR.Application.Abstractions;
using FacturAR.Application.Invitaciones;
using FacturAR.Contracts.Invitaciones;
using FacturAR.Domain.Entities;
using FacturAR.Domain.Enums;
using FacturAR.Domain.Exceptions;
using FacturAR.Domain.Validation;
using FacturAR.Domain.ValueObjects;

namespace FacturAR.UnitTests;

public sealed class ActivacionInvitacionServiceTests
{
    private static readonly string PasswordValida = ContrasenaDePrueba();

    [Theory]
    [InlineData(null, "La contraseña es obligatoria.")]
    [InlineData("", "La contraseña es obligatoria.")]
    [InlineData("123456", "La contraseña debe tener al menos 10 caracteres.")]
    [InlineData("1234567890", "La contraseña debe incluir al menos una letra.")]
    [InlineData("abcdefghij", "La contraseña debe incluir al menos un número.")]
    [InlineData("contrasena1", "La contraseña es demasiado predecible. Elegí otra.")]
    [InlineData("Password123", "La contraseña es demasiado predecible. Elegí otra.")]
    [InlineData("qwerty12345", "La contraseña es demasiado predecible. Elegí otra.")]
    [InlineData("aaaaaaaaa1", "La contraseña es demasiado predecible. Elegí otra.")]
    [InlineData("abcdefghi1", null)]
    [InlineData("0123456789a", null)]
    public void PoliticaContrasena_AplicaLasReglas(string? password, string? esperado)
    {
        Assert.Equal(esperado, PoliticaContrasena.Validar(password));
    }

    [Fact]
    public void PoliticaContrasena_AceptaUnaContrasenaRobusta()
    {
        Assert.Null(PoliticaContrasena.Validar(PasswordValida));
    }

    [Theory]
    [InlineData("abcdefghijklmnop1")]
    [InlineData("9876543210z")]
    public void PoliticaContrasena_AceptaSecuenciasQueNoSonTotalmenteConsecutivas(string password)
    {
        Assert.Null(PoliticaContrasena.Validar(password));
    }

    [Fact]
    public void PoliticaContrasena_RechazaSecuenciasConsecutivasYMuyLargas()
    {
        Assert.NotNull(PoliticaContrasena.Validar("abcdefghijk"));
        Assert.Equal("La contraseña es demasiado larga.", PoliticaContrasena.Validar(new string('a', 120) + "123456789"));
    }

    [Fact]
    public async Task Validar_TokenVigente_DevuelveRazonSocialEmailEnmascaradoYVencimiento()
    {
        var ctx = new Contexto();
        var (token, invitacion) = ctx.AgregarInvitacion();

        var respuesta = await ctx.Validar.ValidarAsync(token);

        Assert.Equal("Demo SRL", respuesta.RazonSocial);
        Assert.Equal("s***@demo.com", respuesta.EmailEnmascarado);
        Assert.Equal(invitacion.ExpiraAt, respuesta.ExpiraAt);
    }

    [Fact]
    public async Task Validar_TokenInexistenteYRevocado_DevuelvenElMismoError()
    {
        var ctx = new Contexto();
        var (revocado, invitacion) = ctx.AgregarInvitacion();
        invitacion.Estado = EstadoInvitacion.Revocada;

        var inexistente = await Assert.ThrowsAsync<InvitacionNoEncontradaException>(
            () => ctx.Validar.ValidarAsync(TokenInvitacion.Generar(DateTime.UtcNow).Token));
        var revocada = await Assert.ThrowsAsync<InvitacionNoEncontradaException>(
            () => ctx.Validar.ValidarAsync(revocado));

        Assert.Equal(inexistente.Message, revocada.Message);
        Assert.DoesNotContain("Demo", revocada.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Validar_TokenVacio_DevuelveNoEncontrada(string? token)
    {
        var ctx = new Contexto();

        await Assert.ThrowsAsync<InvitacionNoEncontradaException>(() => ctx.Validar.ValidarAsync(token));
    }

    [Fact]
    public async Task Validar_TokenVencido_DevuelveVencida()
    {
        var ctx = new Contexto();
        var (token, invitacion) = ctx.AgregarInvitacion();
        invitacion.ExpiraAt = DateTime.UtcNow.AddMinutes(-1);

        await Assert.ThrowsAsync<InvitacionVencidaException>(() => ctx.Validar.ValidarAsync(token));
    }

    [Fact]
    public async Task Validar_TokenUsado_DevuelveUsada()
    {
        var ctx = new Contexto();
        var (token, invitacion) = ctx.AgregarInvitacion();
        invitacion.Estado = EstadoInvitacion.Usada;

        await Assert.ThrowsAsync<InvitacionUsadaException>(() => ctx.Validar.ValidarAsync(token));
    }

    [Fact]
    public async Task Validar_EmpresaInactiva_DevuelveCuentaInactiva()
    {
        var ctx = new Contexto();
        var (token, _) = ctx.AgregarInvitacion();
        ctx.Contribuyente.Activo = false;

        await Assert.ThrowsAsync<CuentaInactivaException>(() => ctx.Validar.ValidarAsync(token));
    }

    [Theory]
    [InlineData("solicitante@demo.com", "s***@demo.com")]
    [InlineData("a@b.ar", "***@b.ar")]
    [InlineData("sin-arroba", "s***")]
    [InlineData("termina@", "t***")]
    [InlineData("a@", "a***")]
    public void EnmascararEmail_MuestraPrimerCaracterYDominio(string email, string esperado)
    {
        Assert.Equal(esperado, InvitacionVigente.EnmascararEmail(email));
    }

    [Fact]
    public async Task Aceptar_TokenVigente_CreaAdministradorConDatosDeLaInvitacionYMarcaUsada()
    {
        var ctx = new Contexto();
        var (token, invitacion) = ctx.AgregarInvitacion();

        var respuesta = await ctx.Aceptar.AceptarAsync(token, new AceptarInvitacionRequest(PasswordValida, PasswordValida));

        Assert.Equal("Cuenta activada. Ya podés iniciar sesión.", respuesta.Mensaje);

        var usuario = Assert.Single(ctx.Usuarios.Insertados);
        Assert.Equal(ctx.Contribuyente.Id, usuario.ContribuyenteId);
        Assert.Equal("solicitante@demo.com", usuario.Email);
        Assert.Equal(RolUsuario.Administrador, usuario.Rol);
        Assert.True(usuario.Activo);
        Assert.NotNull(usuario.EmailVerificadoAt);
        Assert.Equal($"hash:{PasswordValida}", usuario.PasswordHash);

        Assert.Equal(EstadoInvitacion.Usada, invitacion.Estado);
        Assert.NotNull(invitacion.UsadaAt);

        var evento = Assert.Single(ctx.Eventos.Eventos);
        Assert.Equal("cuenta.activada", evento.Accion);
        Assert.Equal(ctx.Contribuyente.Id, evento.ContribuyenteId);
        Assert.Equal(usuario.Id, evento.UsuarioId);
        Assert.Equal("Empresa", evento.ActorTipo);
        Assert.DoesNotContain(PasswordValida, evento.Datos ?? string.Empty);
        Assert.DoesNotContain(token, evento.Datos ?? string.Empty);
    }

    [Theory]
    [InlineData("123456")]
    [InlineData("1234567890")]
    [InlineData("abcdefghij")]
    [InlineData("contrasena1")]
    public async Task Aceptar_PasswordDebil_NoCreaUsuarioYLaInvitacionSigueVigente(string password)
    {
        var ctx = new Contexto();
        var (token, invitacion) = ctx.AgregarInvitacion();

        await Assert.ThrowsAsync<PasswordDebilException>(
            () => ctx.Aceptar.AceptarAsync(token, new AceptarInvitacionRequest(password, password)));

        Assert.Empty(ctx.Usuarios.Insertados);
        Assert.Equal(EstadoInvitacion.Pendiente, invitacion.Estado);
        Assert.Equal(0, ctx.Transaccion.Ejecuciones);
    }

    [Fact]
    public async Task Aceptar_ConfirmacionDistinta_DevuelveValidacionSinCrearUsuario()
    {
        var ctx = new Contexto();
        var (token, invitacion) = ctx.AgregarInvitacion();

        var ex = await Assert.ThrowsAsync<SolicitudAltaValidationException>(
            () => ctx.Aceptar.AceptarAsync(token, new AceptarInvitacionRequest(PasswordValida, PasswordValida + "x")));

        Assert.Equal("Las contraseñas no coinciden.", ex.Message);
        Assert.Empty(ctx.Usuarios.Insertados);
        Assert.Equal(EstadoInvitacion.Pendiente, invitacion.Estado);
    }

    [Fact]
    public async Task Aceptar_DosVeces_LaSegundaDevuelveUsadaSinDuplicar()
    {
        var ctx = new Contexto();
        var (token, _) = ctx.AgregarInvitacion();
        await ctx.Aceptar.AceptarAsync(token, new AceptarInvitacionRequest(PasswordValida, PasswordValida));

        await Assert.ThrowsAsync<InvitacionUsadaException>(
            () => ctx.Aceptar.AceptarAsync(token, new AceptarInvitacionRequest(PasswordValida, PasswordValida)));

        Assert.Single(ctx.Usuarios.Insertados);
    }

    [Fact]
    public async Task Aceptar_CarreraPerdidaEnElCompareAndSet_DevuelveUsada()
    {
        var ctx = new Contexto();
        var (token, _) = ctx.AgregarInvitacion();
        ctx.Invitaciones.PerderCarrera = true;

        await Assert.ThrowsAsync<InvitacionUsadaException>(
            () => ctx.Aceptar.AceptarAsync(token, new AceptarInvitacionRequest(PasswordValida, PasswordValida)));

        Assert.Empty(ctx.Usuarios.Insertados);
    }

    [Fact]
    public async Task Aceptar_UsuarioYaExistenteEnLaEmpresa_DevuelveUsadaYNoTocaLaInvitacion()
    {
        var ctx = new Contexto();
        var (token, invitacion) = ctx.AgregarInvitacion();
        ctx.Usuarios.Existentes.Add((ctx.Contribuyente.Id, "solicitante@demo.com"));

        await Assert.ThrowsAsync<InvitacionUsadaException>(
            () => ctx.Aceptar.AceptarAsync(token, new AceptarInvitacionRequest(PasswordValida, PasswordValida)));

        Assert.Empty(ctx.Usuarios.Insertados);
        Assert.Equal(EstadoInvitacion.Pendiente, invitacion.Estado);
    }

    [Fact]
    public async Task Aceptar_EmpresaInactiva_NoCreaUsuario()
    {
        var ctx = new Contexto();
        var (token, invitacion) = ctx.AgregarInvitacion();
        ctx.Contribuyente.Activo = false;

        await Assert.ThrowsAsync<CuentaInactivaException>(
            () => ctx.Aceptar.AceptarAsync(token, new AceptarInvitacionRequest(PasswordValida, PasswordValida)));

        Assert.Empty(ctx.Usuarios.Insertados);
        Assert.Equal(EstadoInvitacion.Pendiente, invitacion.Estado);
    }

    [Fact]
    public async Task Aceptar_EmpresaDesactivadaDentroDeLaTransaccion_NoCreaUsuario()
    {
        var ctx = new Contexto();
        var (token, invitacion) = ctx.AgregarInvitacion();
        ctx.Contribuyentes.DesactivarEnLaSegundaLectura = true;

        await Assert.ThrowsAsync<CuentaInactivaException>(
            () => ctx.Aceptar.AceptarAsync(token, new AceptarInvitacionRequest(PasswordValida, PasswordValida)));

        Assert.Empty(ctx.Usuarios.Insertados);
        Assert.Equal(EstadoInvitacion.Pendiente, invitacion.Estado);
    }

    [Fact]
    public async Task Aceptar_TokenVencido_DevuelveVencidaSinCrearUsuario()
    {
        var ctx = new Contexto();
        var (token, invitacion) = ctx.AgregarInvitacion();
        invitacion.ExpiraAt = DateTime.UtcNow.AddSeconds(-1);

        await Assert.ThrowsAsync<InvitacionVencidaException>(
            () => ctx.Aceptar.AceptarAsync(token, new AceptarInvitacionRequest(PasswordValida, PasswordValida)));

        Assert.Empty(ctx.Usuarios.Insertados);
    }

    private static string ContrasenaDePrueba()
        => $"Prueba{Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(4))}z7";

    private sealed class Contexto
    {
        public Contexto()
        {
            Validar = new ValidarInvitacionService(Invitaciones, Contribuyentes);
            Aceptar = new AceptarInvitacionService(Invitaciones, Contribuyentes, Usuarios, Eventos, new FakeHasher(), Transaccion);
            Contribuyentes.Todos.Add(Contribuyente);
        }

        public Contribuyente Contribuyente { get; } = new() { RazonSocial = "Demo SRL", Cuit = "20123456786", Activo = true };
        public FakeInvitaciones Invitaciones { get; } = new();
        public FakeContribuyentes Contribuyentes { get; } = new();
        public FakeUsuarios Usuarios { get; } = new();
        public FakeEventos Eventos { get; } = new();
        public FakeTransaccion Transaccion { get; } = new();
        public ValidarInvitacionService Validar { get; }
        public AceptarInvitacionService Aceptar { get; }

        public (string Token, Invitacion Invitacion) AgregarInvitacion()
        {
            var token = TokenInvitacion.Generar(DateTime.UtcNow);
            var invitacion = new Invitacion
            {
                ContribuyenteId = Contribuyente.Id,
                SolicitudId = Guid.NewGuid(),
                Email = "solicitante@demo.com",
                Rol = RolUsuario.Administrador,
                TokenHash = token.Hash,
                TokenPrefix = token.Prefix,
                Estado = EstadoInvitacion.Pendiente,
                ExpiraAt = token.ExpiraAt
            };
            Invitaciones.Todas.Add(invitacion);
            return (token.Token, invitacion);
        }
    }

    private sealed class FakeHasher : IPasswordHasher
    {
        public string Hash(string password) => $"hash:{password}";

        public bool Verify(string password, string hash) => hash == Hash(password);

        public bool NeedsRehash(string hash) => false;
    }

    private sealed class FakeTransaccion : ITransaccionMongo
    {
        public int Ejecuciones { get; private set; }

        public Task<T> EjecutarAsync<T>(Func<CancellationToken, Task<T>> operacion, CancellationToken ct = default)
        {
            Ejecuciones++;
            return operacion(ct);
        }
    }

    private sealed class FakeInvitaciones : IInvitacionRepository
    {
        public List<Invitacion> Todas { get; } = [];

        public bool PerderCarrera { get; set; }

        public Task InsertarAsync(Invitacion invitacion, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<Invitacion?> ObtenerPorTokenHashAsync(string tokenHash, CancellationToken ct = default)
            => Task.FromResult(Todas.FirstOrDefault(i => i.TokenHash == tokenHash));

        public Task<Invitacion?> ObtenerVigentePorSolicitudAsync(Guid solicitudId, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<bool> MarcarUsadaAsync(Guid id, DateTime usadaAt, CancellationToken ct = default)
        {
            var invitacion = Todas.SingleOrDefault(i => i.Id == id && i.Estado == EstadoInvitacion.Pendiente && i.ExpiraAt > usadaAt);
            if (PerderCarrera || invitacion is null)
            {
                return Task.FromResult(false);
            }

            invitacion.Estado = EstadoInvitacion.Usada;
            invitacion.UsadaAt = usadaAt;
            return Task.FromResult(true);
        }

        public Task<bool> RevocarAsync(Guid id, DateTime revocadaAt, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task RegistrarEnvioAsync(Guid id, DateTime enviadoAt, CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    private sealed class FakeContribuyentes : IContribuyenteRepository
    {
        public List<Contribuyente> Todos { get; } = [];

        public int Lecturas { get; private set; }

        public bool DesactivarEnLaSegundaLectura { get; set; }

        public Task<Contribuyente?> GetByIdAsync(Guid id, CancellationToken ct = default)
        {
            Lecturas++;
            var contribuyente = Todos.FirstOrDefault(c => c.Id == id);
            if (contribuyente is null)
            {
                return Task.FromResult<Contribuyente?>(null);
            }

            if (DesactivarEnLaSegundaLectura && Lecturas >= 2)
            {
                return Task.FromResult<Contribuyente?>(new Contribuyente
                {
                    Id = contribuyente.Id,
                    Cuit = contribuyente.Cuit,
                    RazonSocial = contribuyente.RazonSocial,
                    CondicionIva = contribuyente.CondicionIva,
                    Email = contribuyente.Email,
                    Activo = false,
                    CreatedAt = contribuyente.CreatedAt
                });
            }

            return Task.FromResult<Contribuyente?>(contribuyente);
        }

        public Task<bool> ExistePorCuitAsync(string cuit, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task InsertarAsync(Contribuyente contribuyente, CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    private sealed class FakeUsuarios : IUsuarioRepository
    {
        public List<Usuario> Insertados { get; } = [];

        public HashSet<(Guid, string)> Existentes { get; } = [];

        public Task<Usuario?> GetByEmailAsync(string email, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<Usuario?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task InsertAsync(Usuario usuario, CancellationToken ct = default)
        {
            Insertados.Add(usuario);
            return Task.CompletedTask;
        }

        public Task<bool> ExistePorContribuyenteYEmailAsync(Guid contribuyenteId, string email, CancellationToken ct = default)
            => Task.FromResult(
                Existentes.Contains((contribuyenteId, email))
                || Insertados.Any(u => u.ContribuyenteId == contribuyenteId && u.Email == email));

        public Task UpdateAsync(Usuario usuario, CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    private sealed class FakeEventos : IEventoAuditoriaRepository
    {
        public List<EventoAuditoria> Eventos { get; } = [];

        public Task AddAsync(EventoAuditoria evento, CancellationToken ct = default)
        {
            Eventos.Add(evento);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<EventoAuditoria>> ListarPorEntidadAsync(
            string entidad,
            string entidadId,
            CancellationToken ct = default)
            => throw new NotSupportedException();
    }
}
