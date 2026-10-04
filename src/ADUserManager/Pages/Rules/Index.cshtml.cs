using ADUserManager.Data;
using ADUserManager.Services;
using Microsoft.AspNetCore.Mvc;

namespace ADUserManager.Pages.Rules;

public class IndexModel : AppPageModel
{
    private readonly RuleService _rules;
    public IndexModel(RuleService rules) => _rules = rules;

    public List<AccountRule> Rules { get; private set; } = new();

    public async Task OnGetAsync() => Rules = await _rules.GetRulesAsync();

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        try
        {
            await _rules.DeleteRuleAsync(id);
            FlashSuccess("Đã xoá rule.");
        }
        catch (AdOperationException ex)
        {
            FlashError(ex.Message);
        }
        return RedirectToPage();
    }
}
