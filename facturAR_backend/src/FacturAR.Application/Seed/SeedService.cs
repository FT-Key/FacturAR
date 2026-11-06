using System.Globalization;
using FacturAR.Application.Abstractions;
using FacturAR.Application.Parametros;
using FacturAR.Domain.Entities;
using FacturAR.Domain.Enums;
using FacturAR.Domain.Validation;

namespace FacturAR.Application.Seed;

public sealed class SeedService
{
    public const string NombreApiKeyDemo = "API key demo (simulada)";
    public const string DescripcionPuntoVenta = "Casa Central";

    private readonly SeedOptions _options;
    private readonly ParametrosFacturacion _parametros;
    private readonly ISeedRepository _repository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IApiKeyGenerator _apiKeyGenerator;
    private readonly IApiKeyHasher _apiKeyHasher;

    public SeedService(
        SeedOptions options,
        ParametrosFacturacion parametros,
        ISeedRepository repository,
        IPasswordHasher passwordHasher,
        IApiKeyGenerator apiKeyGenerator,
        IApiKeyHasher apiKeyHasher)
    {
        _options = options;
        _parametros = parametros;
        _repository = repository;
        _passwordHasher = passwordHasher;
        _apiKeyGenerator = apiKeyGenerator;
        _apiKeyHasher = apiKeyHasher;
    }

    public async Task<SeedResult> EjecutarAsync(SeedEnvironment entorno, CancellationToken ct = default)
    {
        if (!_options.Enabled)
        {
            return SeedResult.Omitido();
        }

        var error = Validar();
        if (error is not null)
        {
            return SeedResult.Invalido(error);
        }

        var ahora = DateTime.UtcNow;
        var email = NormalizarEmail(_options.Email);
        var passwordHash = _passwordHasher.Hash(_options.Password ?? string.Empty);
        var puntoVenta = PuntoVentaInicial();

        var contribuyente = await _repository.UpsertContribuyenteAsync(
            new Contribuyente
            {
                Cuit = CuitValidator.Normalize(_options.Cuit),
                RazonSocial = _options.RazonSocial.Trim(),
                CondicionIva = CondicionIva.ResponsableInscripto,
                Email = email,
                Activo = true,
                CreatedAt = ahora
            },
            ct);

        await CrearUsuariosAsync(contribuyente.Id, email, passwordHash, ahora, ct);

        await _repository.UpsertAdministradorAsync(
            new AdministradorFacturar
            {
                Nombre = NombreAdministrador(),
                Email = DerivarEmail(email, "superadmin"),
                PasswordHash = passwordHash,
                Rol = RolGlobal.SuperAdmin,
                Activo = true,
                CreatedAt = ahora
            },
            ct);

        await _repository.UpsertPuntoVentaAsync(
            new PuntoVenta
            {
                ContribuyenteId = contribuyente.Id,
                Numero = puntoVenta,
                Descripcion = DescripcionPuntoVenta,
                Activo = true,
                CreatedAt = ahora
            },
            ct);

        foreach (var tipo in new[] { TipoComprobante.FacturaA, TipoComprobante.FacturaB, TipoComprobante.FacturaC })
        {
            await _repository.UpsertSecuenciaAsync(
                new Secuencia
                {
                    ContribuyenteId = contribuyente.Id,
                    PuntoVenta = puntoVenta,
                    TipoComprobante = tipo,
                    UltimoNumero = 0
                },
                ct);
        }

        foreach (var (clave, valor) in _parametros.ObtenerValores())
        {
            await _repository.UpsertParametroAsync(
                new Parametro
                {
                    ContribuyenteId = null,
                    Clave = clave,
                    Valor = valor,
                    UpdatedAt = ahora
                },
                ct);
        }

        string? apiKeyDemoValor = null;

        if (entorno == SeedEnvironment.Development && _options.ApiKeyDemo)
        {
            var generado = _apiKeyGenerator.Generar();
            var creada = await _repository.CrearApiKeySiNoExisteAsync(
                new ApiKey
                {
                    ContribuyenteId = contribuyente.Id,
                    Nombre = NombreApiKeyDemo,
                    Prefix = generado.Prefix,
                    Hash = _apiKeyHasher.Hash(generado.Valor),
                    Activa = true,
                    CreatedAt = ahora
                },
                ct);

            if (creada)
            {
                apiKeyDemoValor = generado.Valor;
            }
        }

        return SeedResult.Completado(apiKeyDemoValor);
    }

    private async Task CrearUsuariosAsync(
        Guid contribuyenteId,
        string email,
        string passwordHash,
        DateTime ahora,
        CancellationToken ct)
    {
        (RolUsuario Rol, string Email)[] usuarios =
        [
            (RolUsuario.Administrador, email),
            (RolUsuario.Emisor, DerivarEmail(email, "emisor")),
            (RolUsuario.Consulta, DerivarEmail(email, "consulta"))
        ];

        foreach (var usuario in usuarios)
        {
            await _repository.UpsertUsuarioAsync(
                new Usuario
                {
                    ContribuyenteId = contribuyenteId,
                    Email = usuario.Email,
                    PasswordHash = passwordHash,
                    Rol = usuario.Rol,
                    Activo = true,
                    EmailVerificadoAt = ahora,
                    CreatedAt = ahora
                },
                ct);
        }
    }

    private string? Validar()
    {
        if (string.IsNullOrWhiteSpace(_options.Password))
        {
            return "Seed__Password es obligatoria cuando Seed__Enabled=true.";
        }

        var errorPassword = PoliticaContrasena.Validar(_options.Password);
        if (errorPassword is not null)
        {
            return errorPassword;
        }

        if (!CuitValidator.IsValid(_options.Cuit))
        {
            return "Seed__Cuit no es un CUIT válido.";
        }

        var email = NormalizarEmail(_options.Email);
        var arroba = email.IndexOf('@');
        if (arroba <= 0 || arroba == email.Length - 1)
        {
            return "Seed__Email debe contener texto antes y después de un '@' para derivar los emails demo.";
        }

        if (string.IsNullOrWhiteSpace(_options.RazonSocial))
        {
            return "Seed__RazonSocial es obligatoria cuando Seed__Enabled=true.";
        }

        return null;
    }

    private int PuntoVentaInicial()
    {
        var valor = _parametros.ObtenerValor(nameof(ParametrosFacturacion.PuntoVentaPorDefecto));

        return int.TryParse(valor, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numero) && numero > 0
            ? numero
            : ParametrosFacturacion.PuntoVentaPredeterminado;
    }

    private string NombreAdministrador()
        => string.IsNullOrWhiteSpace(_options.Nombre) ? "Administrador Demo" : _options.Nombre.Trim();

    private static string NormalizarEmail(string email) => email.Trim().ToLowerInvariant();

    private static string DerivarEmail(string email, string etiqueta)
    {
        var arroba = email.IndexOf('@');
        return $"{email[..arroba]}+{etiqueta}{email[arroba..]}";
    }
}
