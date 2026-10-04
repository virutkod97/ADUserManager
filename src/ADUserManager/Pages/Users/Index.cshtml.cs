using ADUserManager.Data;
using ADUserManager.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ADUserManager.Pages.Users;

public class IndexModel : AppPageModel
{
    private readonly IAdService _ad;
    private readonly RuleService _rules;
    private readonly AuditService _audit;
    private readonly AdOptions _opt;

    public IndexModel(IAdService ad, RuleService rules, AuditService audit, IOptions<AdOptions> opt)
    {
        _ad = ad;
        _rules = rules;
        _audit = audit;
        _opt = opt.Value;
    }

    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true, Name = "rule")] public string? RuleFilter { get; set; }
    [BindProperty(SupportsGet = true, Name = "all")] public bool ShowAll { get; set; }

    public List<AdUser> Users { get; private set; } = new();
    public List<AccountRule> Rules { get; private set; } = new();
    public Dictionary<string, UserRuleAssignment> Assignments { get; private set; } = new();
    public Dictionary<string, List<UserPermissionAssignment>> Permissions { get; private set; } = new();
    public bool Truncated { get; private set; }
    public int HiddenDisabled { get; private set; }
    public string? LoadError { get; private set; }

    public async Task OnGetAsync()
    {
        Rules = await _rules.GetRulesAsync();
        Assignments = await _rules.GetAssignmentsAsync();
        Permissions = await _rules.GetPermissionAssignmentsAsync();
        try
        {
            var all = _ad.SearchUsers(Q);
            Truncated = all.Count >= _opt.MaxSearchResults;
            IEnumerable<AdUser> users = all;
            if (RuleFilter == "none")
                users = users.Where(u => !Assignments.ContainsKey(u.SamAccountName));
            else if (RuleFilter == "due")
                users = users.Where(u => RuleService.GetProbationStatus(Assignments.GetValueOrDefault(u.SamAccountName))?.IsDue == true);
            else if (int.TryParse(RuleFilter, out var ruleId))
                users = users.Where(u => Assignments.GetValueOrDefault(u.SamAccountName)?.RuleId == ruleId
                                         || (Permissions.GetValueOrDefault(u.SamAccountName)?.Any(p => p.RuleId == ruleId) ?? false));
            var list = users.ToList();
            if (!ShowAll)
            {
                HiddenDisabled = list.Count(u => !u.Enabled);
                list = list.Where(u => u.Enabled).ToList();
            }
            Users = list;
        }
        catch (AdOperationException ex)
        {
            LoadError = ex.Message;
        }
    }

    private IActionResult Back() => RedirectToPage(new { q = Q, rule = RuleFilter, all = ShowAll ? "true" : null });

    public async Task<IActionResult> OnPostBulkRuleAsync(Dictionary<string, string?> rules)
    {
        var current = await _rules.GetAssignmentsAsync();
        var mainRules = (await _rules.GetRulesAsync()).Where(r => !r.IsPermission).ToDictionary(r => r.Id);

        var changes = rules
            .Where(kv => int.TryParse(kv.Value, out var id) && mainRules.ContainsKey(id)
                         && current.GetValueOrDefault(kv.Key)?.RuleId != id)
            .Select(kv => (Sam: kv.Key, RuleId: int.Parse(kv.Value!)))
            .ToList();
        if (changes.Count == 0)
        {
            FlashError("Không có thay đổi nào để lưu.");
            return Back();
        }

        var ok = new List<string>();
        var problems = new List<string>();
        foreach (var (sam, ruleId) in changes)
        {
            try
            {
                var result = await _rules.ChangeRuleAsync(sam, ruleId, moveOu: true, addGroups: true, clearGroups: true);
                ok.Add($"{sam} → {mainRules[ruleId].Name} ({result.Summary})");
                problems.AddRange(result.Warnings.Select(w => $"{sam}: {w}"));
            }
            catch (AdOperationException ex)
            {
                problems.Add($"{sam}: {ex.Message}");
            }
        }

        if (ok.Count > 0) FlashSuccess($"Đã đổi rule chính cho {ok.Count}/{changes.Count} tài khoản: {string.Join("; ", ok)}.");
        else FlashError("Không đổi được rule cho tài khoản nào.");
        FlashWarnings(problems);
        return Back();
    }

    public async Task<IActionResult> OnPostToggleAsync(string sam, bool enable)
    {
        try
        {
            _ad.SetEnabled(sam, enable);
            await _audit.LogAsync(enable ? "User.Enable" : "User.Disable", sam);
            FlashSuccess($"Đã {(enable ? "kích hoạt" : "vô hiệu hoá")} tài khoản {sam}.");
        }
        catch (AdOperationException ex)
        {
            await _audit.LogAsync(enable ? "User.Enable" : "User.Disable", sam, ex.Message, false);
            FlashError(ex.Message);
        }
        return Back();
    }

    public async Task<IActionResult> OnPostUnlockAsync(string sam)
    {
        try
        {
            _ad.Unlock(sam);
            await _audit.LogAsync("User.Unlock", sam);
            FlashSuccess($"Đã mở khoá tài khoản {sam}.");
        }
        catch (AdOperationException ex)
        {
            await _audit.LogAsync("User.Unlock", sam, ex.Message, false);
            FlashError(ex.Message);
        }
        return Back();
    }

    public async Task<IActionResult> OnPostDeleteAsync(string sam)
    {
        if (string.Equals(sam, User.Identity?.Name, StringComparison.OrdinalIgnoreCase))
        {
            FlashError("Không thể xoá chính tài khoản đang đăng nhập.");
            return Back();
        }
        try
        {
            await _rules.DeleteUserAsync(sam);
            FlashSuccess($"Đã xoá tài khoản {sam}.");
        }
        catch (AdOperationException ex)
        {
            FlashError(ex.Message);
        }
        return Back();
    }
}
