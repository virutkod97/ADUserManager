using ADUserManager.Data;
using Microsoft.EntityFrameworkCore;

namespace ADUserManager.Services;

public record ProbationStatus(DateTime AssignedAtUtc, DateTime DueAtUtc, int DaysElapsed, int ProbationDays)
{
    public bool IsDue => DateTime.UtcNow >= DueAtUtc;
    public int DaysRemaining => Math.Max(0, (int)Math.Ceiling((DueAtUtc - DateTime.UtcNow).TotalDays));
    public int DaysOverdue => IsDue ? (int)Math.Floor((DateTime.UtcNow - DueAtUtc).TotalDays) : 0;
}

public record ProbationItem(UserRuleAssignment Assignment, AccountRule Rule, ProbationStatus Status);

public class RuleService
{
    private readonly AppDbContext _db;
    private readonly IAdService _ad;
    private readonly AuditService _audit;

    public RuleService(AppDbContext db, IAdService ad, AuditService audit)
    {
        _db = db;
        _ad = ad;
        _audit = audit;
    }

    public Task<List<AccountRule>> GetRulesAsync() =>
        _db.Rules.AsNoTracking().Include(r => r.Assignments).OrderBy(r => r.Name).ToListAsync();

    public Task<AccountRule?> GetRuleAsync(int id) => _db.Rules.FirstOrDefaultAsync(r => r.Id == id);

    public async Task SaveRuleAsync(AccountRule rule)
    {
        if (await _db.Rules.AnyAsync(r => r.Id != rule.Id && r.Name == rule.Name))
            throw new AdOperationException($"Đã có rule tên '{rule.Name}'.");

        var isNew = rule.Id == 0;
        rule.UpdatedAt = DateTime.UtcNow;
        if (isNew)
        {
            rule.CreatedAt = DateTime.UtcNow;
            _db.Rules.Add(rule);
        }
        await _db.SaveChangesAsync();
        await _audit.LogAsync(isNew ? "Rule.Create" : "Rule.Update", rule.Name,
            $"OU: {rule.OuDn}; Groups: {string.Join("; ", rule.GroupDns)}; Thử việc: {(rule.IsProbation ? $"có ({rule.ProbationDays} ngày)" : "không")}");
    }

    public async Task DeleteRuleAsync(int id)
    {
        var rule = await _db.Rules.Include(r => r.Assignments).FirstOrDefaultAsync(r => r.Id == id)
                   ?? throw new AdOperationException("Rule không tồn tại.");
        if (rule.Assignments.Count > 0)
            throw new AdOperationException($"Rule '{rule.Name}' đang được gán cho {rule.Assignments.Count} tài khoản, hãy chuyển các tài khoản sang rule khác trước khi xoá.");
        _db.Rules.Remove(rule);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Rule.Delete", rule.Name);
    }

    public async Task<Dictionary<string, UserRuleAssignment>> GetAssignmentsAsync()
    {
        var list = await _db.Assignments.AsNoTracking().Include(a => a.Rule).ToListAsync();
        return list.ToDictionary(a => a.SamAccountName, StringComparer.OrdinalIgnoreCase);
    }

    public Task<UserRuleAssignment?> GetAssignmentAsync(string sam) =>
        _db.Assignments.Include(a => a.Rule).FirstOrDefaultAsync(a => a.SamAccountName == sam);

    public static ProbationStatus? GetProbationStatus(UserRuleAssignment? a)
    {
        if (a?.Rule is not { IsProbation: true } rule) return null;
        var assigned = a.AssignedAt.AsUtc();
        var due = assigned.AddDays(rule.ProbationDays);
        var elapsed = (int)Math.Floor((DateTime.UtcNow - assigned).TotalDays);
        return new ProbationStatus(assigned, due, Math.Max(0, elapsed), rule.ProbationDays);
    }

    public async Task<List<ProbationItem>> GetProbationItemsAsync()
    {
        var list = await _db.Assignments.AsNoTracking().Include(a => a.Rule)
            .Where(a => a.Rule!.IsProbation).ToListAsync();
        return list.Select(a => new ProbationItem(a, a.Rule!, GetProbationStatus(a)!))
            .OrderByDescending(i => i.Status.IsDue)
            .ThenBy(i => i.Status.DueAtUtc)
            .ToList();
    }

    public async Task<int> CountDueProbationAsync() =>
        (await GetProbationItemsAsync()).Count(i => i.Status.IsDue);

    private async Task UpsertAssignmentAsync(string sam, int ruleId)
    {
        var a = await _db.Assignments.FirstOrDefaultAsync(x => x.SamAccountName == sam);
        if (a is null)
        {
            a = new UserRuleAssignment { SamAccountName = sam };
            _db.Assignments.Add(a);
        }
        a.RuleId = ruleId;
        a.AssignedAt = DateTime.UtcNow;
        a.AssignedBy = _audit.CurrentActor;
        await _db.SaveChangesAsync();
    }

    public async Task RemoveAssignmentAsync(string sam)
    {
        var a = await _db.Assignments.FirstOrDefaultAsync(x => x.SamAccountName == sam);
        if (a is null) return;
        _db.Assignments.Remove(a);
        await _db.SaveChangesAsync();
    }

    public async Task<(AdUser User, List<string> Warnings)> CreateUserAsync(NewUserRequest req, int ruleId)
    {
        var rule = await GetRuleAsync(ruleId) ?? throw new AdOperationException("Rule không tồn tại.");
        req.OuDn = rule.OuDn;

        AdUser user;
        try
        {
            user = _ad.CreateUser(req);
        }
        catch (AdOperationException ex)
        {
            await _audit.LogAsync("User.Create", req.SamAccountName, ex.Message, success: false);
            throw;
        }

        var warnings = new List<string>();
        foreach (var g in rule.GroupDns)
        {
            try { _ad.AddToGroup(user.SamAccountName, g); }
            catch (AdOperationException ex) { warnings.Add(ex.Message); }
        }

        await UpsertAssignmentAsync(user.SamAccountName, rule.Id);
        await _audit.LogAsync("User.Create", user.SamAccountName,
            $"Rule: {rule.Name}; OU: {rule.OuDn}" + (warnings.Count > 0 ? "; Cảnh báo: " + string.Join(" | ", warnings) : ""));
        return (user, warnings);
    }

    public async Task<List<string>> ChangeRuleAsync(string sam, int newRuleId, bool moveOu, bool addGroups, bool removeOldGroups)
    {
        var newRule = await GetRuleAsync(newRuleId) ?? throw new AdOperationException("Rule không tồn tại.");
        var current = await GetAssignmentAsync(sam);
        var oldRule = current?.Rule;
        if (!_ad.UserExists(sam)) throw new AdOperationException($"Không tìm thấy tài khoản '{sam}'.");

        var warnings = new List<string>();
        if (moveOu)
        {
            try { _ad.MoveUser(sam, newRule.OuDn); }
            catch (AdOperationException ex) { warnings.Add(ex.Message); }
        }
        if (removeOldGroups && oldRule is not null)
        {
            foreach (var g in oldRule.GroupDns.Except(newRule.GroupDns, StringComparer.OrdinalIgnoreCase))
            {
                try { _ad.RemoveFromGroup(sam, g); }
                catch (AdOperationException ex) { warnings.Add(ex.Message); }
            }
        }
        if (addGroups)
        {
            foreach (var g in newRule.GroupDns)
            {
                try { _ad.AddToGroup(sam, g); }
                catch (AdOperationException ex) { warnings.Add(ex.Message); }
            }
        }

        await UpsertAssignmentAsync(sam, newRule.Id);
        await _audit.LogAsync("User.ChangeRule", sam,
            $"{oldRule?.Name ?? "(chưa có)"} → {newRule.Name}; Chuyển OU: {moveOu}; Thêm group: {addGroups}; Gỡ group cũ: {removeOldGroups}"
            + (warnings.Count > 0 ? "; Cảnh báo: " + string.Join(" | ", warnings) : ""),
            success: warnings.Count == 0);
        return warnings;
    }

    public async Task DeleteUserAsync(string sam)
    {
        try
        {
            _ad.DeleteUser(sam);
        }
        catch (AdOperationException ex)
        {
            await _audit.LogAsync("User.Delete", sam, ex.Message, success: false);
            throw;
        }
        await RemoveAssignmentAsync(sam);
        await _audit.LogAsync("User.Delete", sam);
    }
}
