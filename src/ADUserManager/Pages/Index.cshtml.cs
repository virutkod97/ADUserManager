using ADUserManager.Services;
using Microsoft.AspNetCore.Mvc;

namespace ADUserManager.Pages;

public class IndexModel : AppPageModel
{
    private readonly RuleService _rules;
    private readonly IAdService _ad;
    private readonly ILogger<IndexModel> _log;

    public IndexModel(RuleService rules, IAdService ad, ILogger<IndexModel> log)
    {
        _rules = rules;
        _ad = ad;
        _log = log;
    }

    public List<ProbationItem> Items { get; private set; } = new();
    public Dictionary<string, string?> Names { get; } = new(StringComparer.OrdinalIgnoreCase);
    public int RuleCount { get; private set; }
    public int AssignedCount { get; private set; }
    public int DueCount => Items.Count(i => i.Status.IsDue);

    public async Task OnGetAsync()
    {
        var rules = await _rules.GetRulesAsync();
        RuleCount = rules.Count;
        AssignedCount = rules.Sum(r => r.Assignments.Count);
        Items = await _rules.GetProbationItemsAsync();

        foreach (var i in Items)
        {
            try { Names[i.Assignment.SamAccountName] = _ad.GetUser(i.Assignment.SamAccountName)?.DisplayName; }
            catch (Exception ex) { _log.LogWarning(ex, "Cannot read {Sam}", i.Assignment.SamAccountName); }
        }
    }

    public async Task<IActionResult> OnPostResetProbationAsync(string sam)
    {
        try
        {
            await _rules.ResetProbationAsync(sam);
            FlashSuccess($"Đã đặt lại thời gian thử việc cho {sam}, bắt đầu đếm lại từ hôm nay.");
        }
        catch (AdOperationException ex)
        {
            FlashError(ex.Message);
        }
        return RedirectToPage();
    }
}
