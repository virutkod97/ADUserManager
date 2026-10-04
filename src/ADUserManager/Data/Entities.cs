using System.ComponentModel.DataAnnotations;

namespace ADUserManager.Data;

public enum RuleKind
{
    Main = 0,
    Permission = 1,
}

public class AccountRule
{
    public int Id { get; set; }

    public RuleKind Kind { get; set; } = RuleKind.Main;

    [Required, MaxLength(100)]
    public string Name { get; set; } = "";

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(1000)]
    public string OuDn { get; set; } = "";

    public List<string> GroupDns { get; set; } = new();

    [MaxLength(1000)]
    public string? PrimaryGroupDn { get; set; }

    public bool IsProbation { get; set; }

    [Range(1, 3650)]
    public int ProbationDays { get; set; } = 7;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<UserRuleAssignment> Assignments { get; set; } = new();
    public List<UserPermissionAssignment> PermissionAssignments { get; set; } = new();

    public bool IsPermission => Kind == RuleKind.Permission;
    public int UserCount => IsPermission ? PermissionAssignments.Count : Assignments.Count;
}

public class UserRuleAssignment
{
    public int Id { get; set; }

    [Required, MaxLength(256)]
    public string SamAccountName { get; set; } = "";

    public int RuleId { get; set; }
    public AccountRule? Rule { get; set; }

    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(256)]
    public string? AssignedBy { get; set; }
}

public class UserPermissionAssignment
{
    public int Id { get; set; }

    [Required, MaxLength(256)]
    public string SamAccountName { get; set; } = "";

    public int RuleId { get; set; }
    public AccountRule? Rule { get; set; }

    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(256)]
    public string? AssignedBy { get; set; }
}

public class AuditLog
{
    public long Id { get; set; }
    public DateTime Time { get; set; } = DateTime.UtcNow;

    [MaxLength(256)]
    public string Actor { get; set; } = "";

    [MaxLength(100)]
    public string Action { get; set; } = "";

    [MaxLength(500)]
    public string? Target { get; set; }

    public string? Details { get; set; }
    public bool Success { get; set; } = true;

    [MaxLength(64)]
    public string? ClientIp { get; set; }
}
