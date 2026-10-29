using FacturAR.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Net.Http.Headers;

namespace FacturAR.Web.Pages;

public sealed class LoginModel : PageModel
{
    private readonly FacturarApiClient _api;

    public LoginModel(FacturarApiClient api)
    {
        _api = api;
    }

    [BindProperty]
    public string? Email { get; set; }

    [BindProperty]
    public string? Password { get; set; }

    public string? Error { get; private set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
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

        RelaySetCookies(cookies);
        return Redirect("/");
    }

    private void RelaySetCookies(IReadOnlyList<string> cookies)
    {
        foreach (var cookie in cookies)
        {
            Response.Headers.Append(HeaderNames.SetCookie, cookie);
        }
    }
}