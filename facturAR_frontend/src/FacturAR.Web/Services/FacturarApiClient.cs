using System.Net;
using System.Net.Http.Json;
using FacturAR.Contracts.Comprobantes;

namespace FacturAR.Web.Services;

public sealed class FacturarApiClient(HttpClient httpClient)
{
    public async Task<VerificacionResponse?> VerificarAsync(string cae, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(
            $"v1/comprobantes/verificar/{Uri.EscapeDataString(cae)}",
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<VerificacionResponse>(cancellationToken);
    }
}
