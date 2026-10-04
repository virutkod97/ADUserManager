using System.ComponentModel.DataAnnotations;
using ADUserManager.Services;
using Microsoft.AspNetCore.Mvc;

namespace ADUserManager.Pages.Users;

public class ResetPasswordModel : AppPageModel
{
    private readonly IAdService _ad;
    private readonly AuditService _audit;

    public ResetPasswordModel(IAdService ad, AuditService audit)
    {
        _ad = ad;
        _audit = audit;
    }

    public class InputModel
    {
        [Required(ErrorMessage = "Nhập mật khẩu mới")]
        public string NewPassword { get; set; } = "";

        public bool MustChange { get; set; } = true;
        public bool Unlock { get; set; } = true;
    }

    [BindProperty(SupportsGet = true)] public string Sam { get; set; } = "";
    [BindProperty] public InputModel Input { get; set; } = new();
    public AdUser? AdUser { get; private set; }
    public bool Done { get; private set; }

    private bool Load()
    {
        try { AdUser = _ad.GetUser(Sam); }
        catch (AdOperationException ex) { FlashError(ex.Message); return false; }
        if (AdUser is null) { FlashError($"Không tìm thấy tài khoản '{Sam}'."); return false; }
        return true;
    }

    public IActionResult OnGet() => Load() ? Page() : RedirectToPage("Index");

    public async Task<IActionResult> OnPostAsync()
    {
        if (!Load()) return RedirectToPage("Index");
        if (!ModelState.IsValid) return Page();
        try
        {
            _ad.ResetPassword(Sam, Input.NewPassword, Input.MustChange, Input.Unlock);
            await _audit.LogAsync("User.ResetPassword", Sam, $"Bắt đổi MK: {Input.MustChange}; Mở khoá: {Input.Unlock}");
            Done = true;
        }
        catch (AdOperationException ex)
        {
            await _audit.LogAsync("User.ResetPassword", Sam, ex.Message, false);
            ModelState.AddModelError("", ex.Message);
        }
        return Page();
    }
}
