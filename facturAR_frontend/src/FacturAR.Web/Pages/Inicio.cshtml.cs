using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FacturAR.Web.Pages;

public sealed class InicioModel : PageModel
{
    public IActionResult OnGet() => RedirectPermanent("/");
}
