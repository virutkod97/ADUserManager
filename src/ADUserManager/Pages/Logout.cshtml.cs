using ADUserManager.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ADUserManager.Pages;

[AllowAnonymous]
public class LogoutModel : PageModel
{
    private readonly AuditService _audit;
    public LogoutModel(AuditService audit) => _audit = audit;

    public IActionResult OnGet() => Redirect("/");

    public async Task<IActionResult> OnPostAsync()
    {
        if (User.Identity?.IsAuthenticated == true) await _audit.LogAsync("Logout", User.Identity.Name);
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Redirect("/Login");
    }
}
