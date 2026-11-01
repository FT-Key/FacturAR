using System.Text;

namespace FacturAR.Api.Seed;

public sealed class SeedApiKeyDemoFile
{
    public const string NombreArchivo = "api-key-demo.txt";
    public const string NombreDirectorio = "seed";

    private static readonly Encoding Codificacion = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private readonly string _ruta;

    public SeedApiKeyDemoFile(IHostEnvironment environment)
        : this(environment.ContentRootPath)
    {
    }

    public SeedApiKeyDemoFile(string contentRootPath)
        => _ruta = Path.Combine(contentRootPath, NombreDirectorio, NombreArchivo);

    public string Ruta => _ruta;

    public async Task EscribirAsync(string valor, CancellationToken ct = default)
    {
        var directorio = Path.GetDirectoryName(_ruta);
        if (!string.IsNullOrEmpty(directorio))
        {
            Directory.CreateDirectory(directorio);
        }

        await File.WriteAllTextAsync(_ruta, valor + Environment.NewLine, Codificacion, ct);
    }
}
