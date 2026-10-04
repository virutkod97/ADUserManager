using System.ComponentModel.DataAnnotations;
using ADUserManager.Data;
using ADUserManager.Services;
using Microsoft.AspNetCore.Mvc;

namespace ADUserManager.Pages.Users;

public class EditModel : AppPageModel
{
    private readonly IAdService _ad;
    private readonly RuleService _rules;
    private readonly AuditService _audit;

    public EditModel(IAdService ad, RuleService rules, AuditService audit)
    {
        _ad = ad;
        _rules = rules;
        _audit = audit;
    }

    public class InputModel
    {
        public string? GivenName { get; set; }
        public string? Surname { get; set; }

        [Required(ErrorMessage = "Nhập tên hiển thị")]
        public string? DisplayName { get; set; }

        [Required(ErrorMessage = "Nhập UPN")]
        [RegularExpression(@"^[^@\s]+@[^@\s]+$", ErrorMessage = "UPN dạng ten@domain")]
        public string? UserPrincipalName { get; set; }

        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        public string? Email { get; set; }

        public string? Description { get; set; }
        public string? Department { get; set; }
        public string? Title { get; set; }
        public string? Phone { get; set; }
        public string? EmployeeId { get; set; }
        public bool PasswordNeverExpires { get; set; }
    }

    [BindProperty] public InputModel Input { get; set; } = new();
    public AdUser? AdUser { get; private set; }
    public UserRuleAssignment? Assignment { get; private set; }
    public List<AccountRule> Rules { get; private set; } = new();

    private async Task<bool> LoadAsync(string sam, bool fillInput)
    {
        try
        {
            AdUser = _ad.GetUser(sam);
        }
        catch (AdOperationException ex)
        {
            FlashError(ex.Message);
            return false;
        }
        if (AdUser is null)
        {
            FlashError($"Không tìm thấy tài khoản '{sam}'.");
            return false;
        }
        Assignment = await _rules.GetAssignmentAsync(AdUser.SamAccountName);
        Rules = await _rules.GetRulesAsync();
        if (fillInput)
        {
            Input = new InputModel
            {
                GivenName = AdUser.GivenName, Surname = AdUser.Surname, DisplayName = AdUser.DisplayName,
                UserPrincipalName = AdUser.UserPrincipalName, Email = AdUser.Email, Description = AdUser.Description,
                Department = AdUser.Department, Title = AdUser.Title, Phone = AdUser.Phone,
                EmployeeId = AdUser.EmployeeId, PasswordNeverExpires = AdUser.PasswordNeverExpires,
            };
        }
        return true;
    }

    public async Task<IActionResult> OnGetAsync(string sam)
    {
        if (string.IsNullOrWhiteSpace(sam) || !await LoadAsync(sam, fillInput: true)) return RedirectToPage("Index");
        return Page();
    }

    private IActionResult Back(string sam) => RedirectToPage(new { sam });

    public async Task<IActionResult> OnPostSaveAsync(string sam)
    {
        if (!ModelState.IsValid)
        {
            if (!await LoadAsync(sam, fillInput: false)) return RedirectToPage("Index");
            return Page();
        }
        try
        {
            _ad.UpdateUser(sam, new UpdateUserRequest
            {
                GivenName = Input.GivenName, Surname = Input.Surname, DisplayName = Input.DisplayName,
                UserPrincipalName = Input.UserPrincipalName, Email = Input.Email, Description = Input.Description,
                Department = Input.Department, Title = Input.Title, Phone = Input.Phone,
                EmployeeId = Input.EmployeeId, PasswordNeverExpires = Input.PasswordNeverExpires,
            });
            await _audit.LogAsync("User.Update", sam);
            FlashSuccess("Đã lưu thông tin tài khoản.");
        }
        catch (AdOperationException ex)
        {
            await _audit.LogAsync("User.Update", sam, ex.Message, false);
            FlashError(ex.Message);
        }
        return Back(sam);
    }

    public async Task<IActionResult> OnPostChangeRuleAsync(string sam, int? newRuleId, bool moveOu, bool addGroups, bool removeOldGroups)
    {
        if (newRuleId is null)
        {
            FlashError("Chọn rule cần chuyển.");
            return Back(sam);
        }
        try
        {
            var warnings = await _rules.ChangeRuleAsync(sam, newRuleId.Value, moveOu, addGroups, removeOldGroups);
            var rule = await _rules.GetRuleAsync(newRuleId.Value);
            FlashSuccess($"Đã chuyển tài khoản {sam} sang rule '{rule?.Name}'.");
            FlashWarnings(warnings);
        }
        catch (AdOperationException ex)
        {
            FlashError(ex.Message);
        }
        return Back(sam);
    }

    public async Task<IActionResult> OnPostRemoveRuleAsync(string sam)
    {
        await _rules.RemoveAssignmentAsync(sam);
        await _audit.LogAsync("User.RemoveRule", sam);
        FlashSuccess("Đã bỏ gán rule.");
        return Back(sam);
    }

    public async Task<IActionResult> OnPostToggleAsync(string sam, bool enable)
    {
        try
        {
            _ad.SetEnabled(sam, enable);
            await _audit.LogAsync(enable ? "User.Enable" : "User.Disable", sam);
            FlashSuccess($"Đã {(enable ? "kích hoạt" : "vô hiệu hoá")} tài khoản.");
        }
        catch (AdOperationException ex)
        {
            await _audit.LogAsync(enable ? "User.Enable" : "User.Disable", sam, ex.Message, false);
            FlashError(ex.Message);
        }
        return Back(sam);
    }

    public async Task<IActionResult> OnPostUnlockAsync(string sam)
    {
        try
        {
            _ad.Unlock(sam);
            await _audit.LogAsync("User.Unlock", sam);
            FlashSuccess("Đã mở khoá tài khoản.");
        }
        catch (AdOperationException ex)
        {
            await _audit.LogAsync("User.Unlock", sam, ex.Message, false);
            FlashError(ex.Message);
        }
        return Back(sam);
    }

    public async Task<IActionResult> OnPostDeleteAsync(string sam)
    {
        if (string.Equals(sam, User.Identity?.Name, StringComparison.OrdinalIgnoreCase))
        {
            FlashError("Không thể xoá chính tài khoản đang đăng nhập.");
            return Back(sam);
        }
        try
        {
            await _rules.DeleteUserAsync(sam);
            FlashSuccess($"Đã xoá tài khoản {sam}.");
            return RedirectToPage("Index");
        }
        catch (AdOperationException ex)
        {
            FlashError(ex.Message);
            return Back(sam);
        }
    }
}
