using FacturAR.Web.Auth;
using FacturAR.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Net.Http.Headers;

namespace FacturAR.Web.Pages;

public sealed class LogoutModel : PageModel
{
    private readonly FacturarApiClient _api;
    private readonly IConfiguration _configuration;

    public LogoutModel(FacturarApiClient api, IConfiguration configuration)
    {
        _api = api;
        _configuration = configuration;
    }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
        => await CerrarSesionAsync(cancellationToken);

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
        => await CerrarSesionAsync(cancellationToken);

    private async Task<IActionResult> CerrarSesionAsync(CancellationToken cancellationToken)
    {
        try
        {
            var cookies = await _api.LogoutEmpresaAsync(
                Request.Headers.Cookie.ToString(),
                cancellationToken);

            foreach (var cookie in cookies)
            {
                Response.Headers.Append(HeaderNames.SetCookie, cookie);
            }
        }
        catch (HttpRequestException)
        {
        }

        SesionPortal.EliminarCookiesDeSesion(
            Response,
            _configuration,
            SesionPortal.NombreContribuyente(_configuration),
            SesionPortal.NombreAdmin(_configuration));

        return Redirect("/login");
    }
}
