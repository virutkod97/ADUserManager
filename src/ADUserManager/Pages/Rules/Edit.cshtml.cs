using System.ComponentModel.DataAnnotations;
using ADUserManager.Data;
using ADUserManager.Services;
using Microsoft.AspNetCore.Mvc;

namespace ADUserManager.Pages.Rules;

public class EditModel : AppPageModel
{
    private readonly RuleService _rules;
    private readonly IAdService _ad;

    public EditModel(RuleService rules, IAdService ad)
    {
        _rules = rules;
        _ad = ad;
    }

    public class InputModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Nhập tên rule"), MaxLength(100)]
        public string Name { get; set; } = "";

        [MaxLength(500)]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Chọn OU")]
        public string OuDn { get; set; } = "";

        public List<string> GroupDns { get; set; } = new();

        public bool IsProbation { get; set; }

        [Range(1, 3650, ErrorMessage = "Số ngày từ 1 đến 3650")]
        public int ProbationDays { get; set; } = 7;
    }

    [BindProperty] public InputModel Input { get; set; } = new();
    public IReadOnlyList<AdOu> Ous { get; private set; } = Array.Empty<AdOu>();
    public IReadOnlyList<AdGroup> Groups { get; private set; } = Array.Empty<AdGroup>();
    public string? LoadError { get; private set; }

    private void LoadLookups()
    {
        try
        {
            Ous = _ad.GetOrganizationalUnits();
            Groups = _ad.GetGroups();
        }
        catch (AdOperationException ex)
        {
            LoadError = ex.Message;
        }
    }

    public async Task<IActionResult> OnGetAsync(int? id)
    {
        if (id is > 0)
        {
            var r = await _rules.GetRuleAsync(id.Value);
            if (r is null) return NotFound();
            Input = new InputModel
            {
                Id = r.Id, Name = r.Name, Description = r.Description, OuDn = r.OuDn,
                GroupDns = r.GroupDns.ToList(), IsProbation = r.IsProbation, ProbationDays = r.ProbationDays,
            };
        }
        LoadLookups();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            LoadLookups();
            return Page();
        }

        AccountRule rule;
        if (Input.Id > 0)
        {
            rule = await _rules.GetRuleAsync(Input.Id) ?? throw new InvalidOperationException();
        }
        else rule = new AccountRule();

        rule.Name = Input.Name.Trim();
        rule.Description = Input.Description?.Trim();
        rule.OuDn = Input.OuDn;
        rule.GroupDns = Input.GroupDns.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        rule.IsProbation = Input.IsProbation;
        rule.ProbationDays = Input.ProbationDays;

        try
        {
            await _rules.SaveRuleAsync(rule);
        }
        catch (AdOperationException ex)
        {
            ModelState.AddModelError("", ex.Message);
            LoadLookups();
            return Page();
        }

        FlashSuccess($"Đã lưu rule '{rule.Name}'.");
        return RedirectToPage("Index");
    }
}
