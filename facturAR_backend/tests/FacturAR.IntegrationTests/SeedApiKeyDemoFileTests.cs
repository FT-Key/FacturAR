using FacturAR.Api.Seed;

namespace FacturAR.IntegrationTests;

public sealed class SeedApiKeyDemoFileTests
{
    [Fact]
    public async Task EscribirAsync_CreaElArchivoEnLaCarpetaSeed()
    {
        var directorio = Path.Combine(Path.GetTempPath(), $"facturar_seed_{Guid.NewGuid():N}");

        try
        {
            var archivo = new SeedApiKeyDemoFile(directorio);
            var valor = $"fak_{Guid.NewGuid():N}";

            await archivo.EscribirAsync(valor);

            Assert.Equal(
                Path.Combine(directorio, "seed", "api-key-demo.txt"),
                archivo.Ruta);
            Assert.True(File.Exists(archivo.Ruta));
            Assert.Equal(valor, (await File.ReadAllTextAsync(archivo.Ruta)).Trim());
        }
        finally
        {
            if (Directory.Exists(directorio))
            {
                Directory.Delete(directorio, recursive: true);
            }
        }
    }

    [Fact]
    public void Ruta_SeConstruyeBajoLaRaizDeContenido()
    {
        var raiz = Path.Combine(Path.GetTempPath(), "contenido-facturar");
        var archivo = new SeedApiKeyDemoFile(raiz);

        Assert.Equal(Path.Combine(raiz, "seed", "api-key-demo.txt"), archivo.Ruta);
        Assert.Equal("api-key-demo.txt", SeedApiKeyDemoFile.NombreArchivo);
        Assert.Equal("seed", SeedApiKeyDemoFile.NombreDirectorio);
    }
}
