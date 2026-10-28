using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HomecrestWeatherAlert.Areas.Identity.Pages.Account;

[AllowAnonymous]
public class RegisterModel : PageModel
{
    public IActionResult OnGet()
    {
        // Public self-registration is disabled; admins create logins under /Admin/Users.
        return RedirectToPage("/Account/Login", new { area = "Identity" });
    }

    public IActionResult OnPost()
    {
        return RedirectToPage("/Account/Login", new { area = "Identity" });
    }
}
