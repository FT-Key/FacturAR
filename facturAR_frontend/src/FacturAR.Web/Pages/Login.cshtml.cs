using FacturAR.Web.Auth;
using FacturAR.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Net.Http.Headers;

namespace FacturAR.Web.Pages;

public sealed class LoginModel : PageModel
{
    private readonly FacturarApiClient _api;
    private readonly IConfiguration _configuration;

    public LoginModel(FacturarApiClient api, IConfiguration configuration)
    {
        _api = api;
        _configuration = configuration;
    }

    [BindProperty]
    public string? Email { get; set; }

    [BindProperty]
    public string? Password { get; set; }

    public string? Error { get; private set; }

    [BindProperty(SupportsGet = true, Name = "exito")]
    public string? Exito { get; set; }

    public bool CuentaActivada => string.Equals(Exito, "cuenta-activada", StringComparison.Ordinal);

    public IActionResult OnGet()
        => User.Identity?.IsAuthenticated == true
            ? Redirect(SesionPortal.PanelUrl(User))
            : Page();

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return Redirect(SesionPortal.PanelUrl(User));
        }

        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrEmpty(Password))
        {
            Error = "Ingresá tu email y tu contraseña.";
            return Page();
        }

        IReadOnlyList<string>? cookies;
        try
        {
            cookies = await _api.LoginEmpresaAsync(Email, Password, cancellationToken);
        }
        catch (HttpRequestException)
        {
            Error = "No se pudo contactar el servicio de autenticación. Intentá nuevamente.";
            return Page();
        }

        if (cookies is null)
        {
            Error = "Email o contraseña incorrectos.";
            return Page();
        }

        SesionPortal.EliminarCookiesDeSesion(
            Response,
            _configuration,
            SesionPortal.NombreAdmin(_configuration));

        RelaySetCookies(cookies);
        return Redirect("/dashboard");
    }

    private void RelaySetCookies(IReadOnlyList<string> cookies)
    {
        foreach (var cookie in cookies)
        {
            Response.Headers.Append(HeaderNames.SetCookie, cookie);
        }
    }
}
