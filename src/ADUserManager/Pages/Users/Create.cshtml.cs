using System.ComponentModel.DataAnnotations;
using ADUserManager.Data;
using ADUserManager.Services;
using Microsoft.AspNetCore.Mvc;

namespace ADUserManager.Pages.Users;

public class CreateModel : AppPageModel
{
    private readonly IAdService _ad;
    private readonly RuleService _rules;

    public CreateModel(IAdService ad, RuleService rules)
    {
        _ad = ad;
        _rules = rules;
    }

    public class InputModel
    {
        [Required(ErrorMessage = "Chọn rule")]
        public int? RuleId { get; set; }

        [MaxLength(64)]
        public string? Surname { get; set; }

        [Required(ErrorMessage = "Nhập tên"), MaxLength(64)]
        public string GivenName { get; set; } = "";

        [Required(ErrorMessage = "Nhập tên hiển thị"), MaxLength(256)]
        public string DisplayName { get; set; } = "";

        [Required(ErrorMessage = "Nhập tên đăng nhập")]
        [RegularExpression(@"^[A-Za-z0-9][A-Za-z0-9._\-]{0,19}$",
            ErrorMessage = "Tối đa 20 ký tự: chữ không dấu, số, dấu chấm, gạch dưới, gạch ngang")]
        public string SamAccountName { get; set; } = "";

        public string? UpnSuffix { get; set; }

        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        public string? Email { get; set; }

        public string? EmployeeId { get; set; }
        public string? Phone { get; set; }
        public string? Department { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }

        [Required(ErrorMessage = "Nhập mật khẩu")]
        public string Password { get; set; } = "";

        public bool MustChangePassword { get; set; } = true;
        public bool Enabled { get; set; } = true;
    }

    [BindProperty] public InputModel Input { get; set; } = new();
    public List<AccountRule> Rules { get; private set; } = new();
    public IReadOnlyList<string> UpnSuffixes { get; private set; } = Array.Empty<string>();

    private async Task LoadAsync()
    {
        Rules = await _rules.GetRulesAsync();
        try { UpnSuffixes = _ad.GetUpnSuffixes(); }
        catch (AdOperationException ex) { ModelState.AddModelError("", ex.Message); }
    }

    public async Task OnGetAsync(int? rule)
    {
        Input.RuleId = rule;
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadAsync();
        if (!ModelState.IsValid) return Page();

        var suffix = string.IsNullOrWhiteSpace(Input.UpnSuffix) ? _ad.DomainName : Input.UpnSuffix.Trim();
        var req = new NewUserRequest
        {
            SamAccountName = Input.SamAccountName.Trim(),
            UserPrincipalName = $"{Input.SamAccountName.Trim()}@{suffix}",
            GivenName = Input.GivenName,
            Surname = Input.Surname,
            DisplayName = Input.DisplayName,
            Email = Input.Email,
            EmployeeId = Input.EmployeeId,
            Phone = Input.Phone,
            Department = Input.Department,
            Title = Input.Title,
            Description = Input.Description,
            Password = Input.Password,
            MustChangePassword = Input.MustChangePassword,
            Enabled = Input.Enabled,
        };

        try
        {
            var (user, warnings) = await _rules.CreateUserAsync(req, Input.RuleId!.Value);
            FlashSuccess($"Đã tạo tài khoản {user.SamAccountName} ({user.DisplayName}).");
            FlashWarnings(warnings);
            return RedirectToPage("Edit", new { sam = user.SamAccountName });
        }
        catch (AdOperationException ex)
        {
            ModelState.AddModelError("", ex.Message);
            return Page();
        }
    }
}
