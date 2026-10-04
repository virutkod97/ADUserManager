using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using ADUserManager.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ADUserManager.Pages;

public class LoginModel : PageModel
{
    private readonly IAdService _ad;
    private readonly AuditService _audit;
    private readonly LoginThrottle _throttle;

    public LoginModel(IAdService ad, AuditService audit, LoginThrottle throttle)
    {
        _ad = ad;
        _audit = audit;
        _throttle = throttle;
    }

    public class InputModel
    {
        [Required(ErrorMessage = "Nhập tài khoản")]
        public string Username { get; set; } = "";

        [Required(ErrorMessage = "Nhập mật khẩu")]
        public string Password { get; set; } = "";
    }

    [BindProperty] public InputModel Input { get; set; } = new();
    public string? ReturnUrl { get; set; }
    public string? Error { get; set; }
    public string Domain { get; set; } = "";

    public void OnGet(string? returnUrl)
    {
        ReturnUrl = returnUrl;
        Domain = SafeDomain();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl)
    {
        ReturnUrl = returnUrl;
        Domain = SafeDomain();
        if (!ModelState.IsValid) return Page();

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var key = LoginThrottle.Key(ip, Input.Username);
        if (_throttle.IsBlocked(key, out var remaining))
        {
            Error = $"Đăng nhập sai quá nhiều lần. Thử lại sau {Math.Ceiling(remaining.TotalMinutes)} phút.";
            return Page();
        }

        var result = await Task.Run(() => _ad.AuthenticateAdmin(Input.Username, Input.Password));
        if (!result.Success)
        {
            _throttle.Fail(key);
            Error = result.Status switch
            {
                AuthStatus.NotAuthorized => "Tài khoản không thuộc nhóm Administrators nên không được phép sử dụng phần mềm.",
                AuthStatus.Error => "Không kết nối được Active Directory: " + result.Error,
                _ => "Sai tài khoản hoặc mật khẩu.",
            };
            await _audit.LogAsync("Login", Input.Username, result.Status.ToString(), success: false, actor: Input.Username);
            return Page();
        }

        _throttle.Success(key);
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, result.SamAccountName!),
            new("display", result.DisplayName ?? result.SamAccountName!),
            new(ClaimTypes.Role, AppRoles.Admin),
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
        await _audit.LogAsync("Login", result.SamAccountName, actor: result.SamAccountName);

        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : "/");
    }

    private string SafeDomain()
    {
        try { return _ad.DomainName; }
        catch { return ""; }
    }
}
