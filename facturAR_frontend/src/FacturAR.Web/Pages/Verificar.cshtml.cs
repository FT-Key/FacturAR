using FacturAR.Contracts.Comprobantes;
using FacturAR.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FacturAR.Web.Pages;

public sealed class VerificarModel : PageModel
{
    private readonly FacturarApiClient _api;

    public VerificarModel(FacturarApiClient api)
    {
        _api = api;
    }

    [BindProperty(SupportsGet = true, Name = "cae")]
    public string? Cae { get; set; }

    public VerificacionResponse? Comprobante { get; private set; }
    public string? Error { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(Cae))
        {
            return;
        }

        Cae = Normalizar(Cae);
        if (Cae.Length != 14 || !Cae.All(char.IsAsciiDigit))
        {
            Error = "El CAE debe contener exactamente 14 dígitos.";
            return;
        }

        try
        {
            Comprobante = await _api.VerificarAsync(Cae, cancellationToken);
            if (Comprobante is null)
            {
                Error = "No se encontró un comprobante emitido con el CAE indicado.";
            }
        }
        catch (HttpRequestException)
        {
            Error = "No se pudo consultar el servicio de verificación. Intentá nuevamente.";
        }
    }

    private static string Normalizar(string? cae)
    {
        if (string.IsNullOrWhiteSpace(cae))
        {
            return string.Empty;
        }

        return new string(cae.Trim().Where(c => c is not ('-' or '.' or '/' or ' ')).ToArray());
    }
}
