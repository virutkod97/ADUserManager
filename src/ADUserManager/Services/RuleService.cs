using ADUserManager.Data;
using Microsoft.EntityFrameworkCore;

namespace ADUserManager.Services;

public record ProbationStatus(DateTime AssignedAtUtc, DateTime DueAtUtc, int DaysElapsed, int ProbationDays)
{
    public bool IsDue => DateTime.UtcNow >= DueAtUtc;
    public int DaysRemaining => Math.Max(0, (int)Math.Ceiling((DueAtUtc - DateTime.UtcNow).TotalDays));
    public int DaysOverdue => IsDue ? (int)Math.Floor((DateTime.UtcNow - DueAtUtc).TotalDays) : 0;
}

public record RuleChangeResult(List<string> Before, List<string> Removed, List<string> Kept, List<string> Warnings, string? PrimaryChange)
{
    public string Summary =>
        $"đọc được {Before.Count} group: {(Before.Count > 0 ? string.Join(", ", Before) : "(trống)")}; "
        + (PrimaryChange is not null ? $"primary: {PrimaryChange}; " : "")
        + $"gỡ: {(Removed.Count > 0 ? string.Join(", ", Removed) : "(không)")}"
        + (Kept.Count > 0 ? $"; giữ: {string.Join(", ", Kept)}" : "");
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
        _db.Rules.AsNoTracking()
            .Include(r => r.Assignments)
            .Include(r => r.PermissionAssignments)
            .OrderBy(r => r.Name)
            .ToListAsync();

    public Task<AccountRule?> GetRuleAsync(int id) => _db.Rules.FirstOrDefaultAsync(r => r.Id == id);

    public async Task SaveRuleAsync(AccountRule rule)
    {
        if (await _db.Rules.AnyAsync(r => r.Id != rule.Id && r.Name == rule.Name))
            throw new AdOperationException($"Đã có rule tên '{rule.Name}'.");

        var isNew = rule.Id == 0;
        if (!isNew)
        {
            var usedAsMain = await _db.Assignments.AnyAsync(a => a.RuleId == rule.Id);
            var usedAsPermission = await _db.PermissionAssignments.AnyAsync(a => a.RuleId == rule.Id);
            if ((rule.IsPermission && usedAsMain) || (!rule.IsPermission && usedAsPermission))
                throw new AdOperationException("Không thể đổi loại rule khi rule đang được gán cho tài khoản.");
        }
        if (rule.IsPermission)
        {
            rule.OuDn = "";
            rule.IsProbation = false;
            rule.PrimaryGroupDn = null;
        }
        else if (!string.IsNullOrEmpty(rule.PrimaryGroupDn) && !rule.GroupDns.Contains(rule.PrimaryGroupDn, StringComparer.OrdinalIgnoreCase))
        {
            rule.GroupDns.Add(rule.PrimaryGroupDn);
        }
        else if (string.IsNullOrWhiteSpace(rule.OuDn))
        {
            throw new AdOperationException("Rule chính phải chọn OU.");
        }
        rule.UpdatedAt = DateTime.UtcNow;
        if (isNew)
        {
            rule.CreatedAt = DateTime.UtcNow;
            _db.Rules.Add(rule);
        }
        await _db.SaveChangesAsync();
        await _audit.LogAsync(isNew ? "Rule.Create" : "Rule.Update", rule.Name,
            rule.IsPermission
                ? $"Rule phân quyền; Groups: {string.Join("; ", rule.GroupDns)}"
                : $"Rule chính; OU: {rule.OuDn}; Groups: {string.Join("; ", rule.GroupDns)}; Primary: {rule.PrimaryGroupDn ?? "Domain Users"}; Thử việc: {(rule.IsProbation ? $"có ({rule.ProbationDays} ngày)" : "không")}");
    }

    public async Task DeleteRuleAsync(int id)
    {
        var rule = await _db.Rules.Include(r => r.Assignments).Include(r => r.PermissionAssignments)
                       .FirstOrDefaultAsync(r => r.Id == id)
                   ?? throw new AdOperationException("Rule không tồn tại.");
        if (rule.UserCount > 0)
            throw new AdOperationException(rule.IsPermission
                ? $"Rule '{rule.Name}' đang được gán cho {rule.UserCount} tài khoản, hãy gỡ rule khỏi các tài khoản trước khi xoá."
                : $"Rule '{rule.Name}' đang được gán cho {rule.UserCount} tài khoản, hãy chuyển các tài khoản sang rule khác trước khi xoá.");
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

    public async Task<(AdUser User, List<string> Warnings)> CreateUserAsync(
        NewUserRequest req, int ruleId, IEnumerable<int>? permissionRuleIds = null)
    {
        var rule = await GetRuleAsync(ruleId) ?? throw new AdOperationException("Rule không tồn tại.");
        if (rule.IsPermission) throw new AdOperationException("Phải chọn rule chính khi tạo tài khoản.");
        var permIds = (permissionRuleIds ?? Enumerable.Empty<int>()).Distinct().ToList();
        var permRules = await _db.Rules.Where(r => permIds.Contains(r.Id) && r.Kind == RuleKind.Permission).ToListAsync();
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

        if (!string.IsNullOrEmpty(rule.PrimaryGroupDn))
        {
            try { _ad.SetPrimaryGroup(user.SamAccountName, rule.PrimaryGroupDn); }
            catch (AdOperationException ex) { warnings.Add(ex.Message); }
        }

        await UpsertAssignmentAsync(user.SamAccountName, rule.Id);
        foreach (var pr in permRules)
            warnings.AddRange(await GrantPermissionAsync(user.SamAccountName, pr));
        await _audit.LogAsync("User.Create", user.SamAccountName,
            $"Rule: {rule.Name}; OU: {rule.OuDn}"
            + (permRules.Count > 0 ? "; Phân quyền: " + string.Join(", ", permRules.Select(r => r.Name)) : "")
            + (warnings.Count > 0 ? "; Cảnh báo: " + string.Join(" | ", warnings) : ""));
        return (user, warnings);
    }

    // Thứ tự: lưu danh sách group cần gỡ (gồm cả primary group) → thêm group rule mới
    // → đặt primary group theo rule (Domain Users nếu rule không chọn) → gỡ các group đã lưu.
    // AD không cho gỡ primary group trực tiếp; sau khi đổi primary, group primary cũ thành thành viên thường.
    public async Task<RuleChangeResult> ChangeRuleAsync(string sam, int newRuleId, bool moveOu, bool addGroups, bool clearGroups)
    {
        var newRule = await GetRuleAsync(newRuleId) ?? throw new AdOperationException("Rule không tồn tại.");
        if (newRule.IsPermission) throw new AdOperationException("Rule phân quyền được gán ở mục 'Rule phân quyền', không dùng làm rule chính.");
        var current = await GetAssignmentAsync(sam);
        var oldRule = current?.Rule;
        var user = _ad.GetUser(sam) ?? throw new AdOperationException($"Không tìm thấy tài khoản '{sam}'.");
        var groupsBefore = _ad.GetUserGroups(sam);
        var targetPrimary = string.IsNullOrEmpty(newRule.PrimaryGroupDn) ? _ad.GetDomainUsersDn() : newRule.PrimaryGroupDn;
        static string Cn(string dn) => DnHelper.RdnValue(DnHelper.Split(dn)[0]);

        var warnings = new List<string>();
        if (moveOu)
        {
            try { _ad.MoveUser(sam, newRule.OuDn); }
            catch (AdOperationException ex) { warnings.Add(ex.Message); }
        }

        var kept = new List<string>();
        var toRemove = new List<string>();
        if (clearGroups)
        {
            var keepReason = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var g in newRule.GroupDns) keepReason[g] = "rule mới";
            keepReason.TryAdd(targetPrimary, "primary group mới");
            foreach (var p in await GetUserPermissionsAsync(sam))
                foreach (var g in p.Rule!.GroupDns) keepReason.TryAdd(g, $"rule phân quyền {p.Rule.Name}");

            foreach (var g in groupsBefore)
            {
                if (keepReason.TryGetValue(g, out var why)) kept.Add($"{Cn(g)} ({why})");
                else toRemove.Add(g);
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

        string? primaryChange = null;
        if (!string.Equals(user.PrimaryGroupDn, targetPrimary, StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                _ad.AddToGroup(sam, targetPrimary);
                _ad.SetPrimaryGroup(sam, targetPrimary);
                primaryChange = $"{(user.PrimaryGroupDn is null ? "?" : Cn(user.PrimaryGroupDn))} → {Cn(targetPrimary)}";
            }
            catch (AdOperationException ex) { warnings.Add(ex.Message); }
        }

        var removed = new List<string>();
        foreach (var g in toRemove)
        {
            try
            {
                _ad.RemoveFromGroup(sam, g);
                removed.Add(Cn(g));
            }
            catch (AdOperationException ex) { warnings.Add(ex.Message); }
        }

        await UpsertAssignmentAsync(sam, newRule.Id);
        await _audit.LogAsync("User.ChangeRule", sam,
            $"{oldRule?.Name ?? "(chưa có)"} → {newRule.Name}; Chuyển OU: {moveOu}; Thêm group: {addGroups}; "
            + $"Group trước khi đổi: {(groupsBefore.Count > 0 ? string.Join(", ", groupsBefore.Select(Cn)) : "(trống)")}; "
            + (primaryChange is not null ? $"Primary group: {primaryChange}; " : "")
            + (clearGroups ? $"Đã gỡ group: {(removed.Count > 0 ? string.Join(", ", removed) : "(không có)")}" : "Không gỡ group")
            + (kept.Count > 0 ? $"; Giữ: {string.Join(", ", kept)}" : "")
            + (warnings.Count > 0 ? "; Cảnh báo: " + string.Join(" | ", warnings) : ""),
            success: warnings.Count == 0);
        return new RuleChangeResult(groupsBefore.Select(Cn).ToList(), removed, kept, warnings, primaryChange);
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
        _db.PermissionAssignments.RemoveRange(_db.PermissionAssignments.Where(a => a.SamAccountName == sam));
        await _db.SaveChangesAsync();
        await _audit.LogAsync("User.Delete", sam);
    }

    public async Task<Dictionary<string, List<UserPermissionAssignment>>> GetPermissionAssignmentsAsync()
    {
        var list = await _db.PermissionAssignments.AsNoTracking().Include(a => a.Rule).ToListAsync();
        return list.GroupBy(a => a.SamAccountName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderBy(a => a.Rule!.Name).ToList(), StringComparer.OrdinalIgnoreCase);
    }

    public Task<List<UserPermissionAssignment>> GetUserPermissionsAsync(string sam) =>
        _db.PermissionAssignments.AsNoTracking().Include(a => a.Rule)
            .Where(a => a.SamAccountName == sam)
            .OrderBy(a => a.Rule!.Name)
            .ToListAsync();

    private async Task<List<string>> GrantPermissionAsync(string sam, AccountRule rule)
    {
        var warnings = new List<string>();
        foreach (var g in rule.GroupDns)
        {
            try { _ad.AddToGroup(sam, g); }
            catch (AdOperationException ex) { warnings.Add(ex.Message); }
        }
        if (!await _db.PermissionAssignments.AnyAsync(a => a.SamAccountName == sam && a.RuleId == rule.Id))
        {
            _db.PermissionAssignments.Add(new UserPermissionAssignment
            {
                SamAccountName = sam,
                RuleId = rule.Id,
                AssignedAt = DateTime.UtcNow,
                AssignedBy = _audit.CurrentActor,
            });
            await _db.SaveChangesAsync();
        }
        return warnings;
    }

    public async Task<List<string>> AddPermissionAsync(string sam, int ruleId)
    {
        var rule = await GetRuleAsync(ruleId) ?? throw new AdOperationException("Rule không tồn tại.");
        if (!rule.IsPermission) throw new AdOperationException($"'{rule.Name}' không phải rule phân quyền.");
        if (!_ad.UserExists(sam)) throw new AdOperationException($"Không tìm thấy tài khoản '{sam}'.");

        var warnings = await GrantPermissionAsync(sam, rule);
        await _audit.LogAsync("User.AddPermission", sam,
            $"{rule.Name}; Groups: {string.Join("; ", rule.GroupDns)}"
            + (warnings.Count > 0 ? "; Cảnh báo: " + string.Join(" | ", warnings) : ""),
            success: warnings.Count == 0);
        return warnings;
    }

    public async Task<List<string>> RemovePermissionAsync(string sam, int ruleId, bool removeGroups)
    {
        var a = await _db.PermissionAssignments.Include(x => x.Rule)
                    .FirstOrDefaultAsync(x => x.SamAccountName == sam && x.RuleId == ruleId)
                ?? throw new AdOperationException("Tài khoản không có rule phân quyền này.");
        var rule = a.Rule!;

        var warnings = new List<string>();
        if (removeGroups)
        {
            // Group vẫn được rule chính hoặc rule phân quyền khác cấp thì giữ lại
            var keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if ((await GetAssignmentAsync(sam))?.Rule is { } main) keep.UnionWith(main.GroupDns);
            foreach (var other in await GetUserPermissionsAsync(sam))
                if (other.RuleId != ruleId) keep.UnionWith(other.Rule!.GroupDns);

            foreach (var g in rule.GroupDns.Where(g => !keep.Contains(g)))
            {
                try { _ad.RemoveFromGroup(sam, g); }
                catch (AdOperationException ex) { warnings.Add(ex.Message); }
            }
        }

        _db.PermissionAssignments.Remove(a);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("User.RemovePermission", sam,
            $"{rule.Name}; Gỡ group: {removeGroups}" + (warnings.Count > 0 ? "; Cảnh báo: " + string.Join(" | ", warnings) : ""),
            success: warnings.Count == 0);
        return warnings;
    }
}
