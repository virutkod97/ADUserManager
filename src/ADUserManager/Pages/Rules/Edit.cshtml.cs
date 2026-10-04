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

        public RuleKind Kind { get; set; } = RuleKind.Main;

        [Required(ErrorMessage = "Nhập tên rule"), MaxLength(100)]
        public string Name { get; set; } = "";

        [MaxLength(500)]
        public string? Description { get; set; }

        public string? OuDn { get; set; }

        public List<string> GroupDns { get; set; } = new();

        public string? PrimaryGroupDn { get; set; }

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

    public async Task<IActionResult> OnGetAsync(int? id, RuleKind? kind)
    {
        if (kind is not null) Input.Kind = kind.Value;
        if (id is > 0)
        {
            var r = await _rules.GetRuleAsync(id.Value);
            if (r is null) return NotFound();
            Input = new InputModel
            {
                Id = r.Id, Kind = r.Kind, Name = r.Name, Description = r.Description, OuDn = r.OuDn,
                GroupDns = r.GroupDns.ToList(), PrimaryGroupDn = r.PrimaryGroupDn, IsProbation = r.IsProbation, ProbationDays = r.ProbationDays,
            };
        }
        LoadLookups();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (Input.Kind == RuleKind.Main && string.IsNullOrWhiteSpace(Input.OuDn))
            ModelState.AddModelError("Input.OuDn", "Chọn OU");
        if (Input.Kind == RuleKind.Permission && Input.GroupDns.Count == 0)
            ModelState.AddModelError("", "Rule phân quyền phải có ít nhất 1 group.");
        if (Input.Kind == RuleKind.Main && !string.IsNullOrEmpty(Input.PrimaryGroupDn))
        {
            LoadLookups();
            var g = Groups.FirstOrDefault(x => string.Equals(x.DistinguishedName, Input.PrimaryGroupDn, StringComparison.OrdinalIgnoreCase));
            if (g is null || !g.CanBePrimary)
                ModelState.AddModelError("Input.PrimaryGroupDn", "Group này không dùng được làm primary group (chỉ group bảo mật Global/Universal).");
        }
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

        rule.Kind = Input.Kind;
        rule.Name = Input.Name.Trim();
        rule.Description = Input.Description?.Trim();
        rule.OuDn = Input.OuDn ?? "";
        rule.GroupDns = Input.GroupDns.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        rule.PrimaryGroupDn = string.IsNullOrWhiteSpace(Input.PrimaryGroupDn) ? null : Input.PrimaryGroupDn;
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
