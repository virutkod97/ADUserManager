using System.ComponentModel.DataAnnotations;

namespace ADUserManager.Data;

/// <summary>
/// Rule tài khoản: quy định tài khoản thuộc rule sẽ nằm ở OU nào và là thành viên những group nào.
/// </summary>
public class AccountRule
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = "";

    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>DistinguishedName của OU đích.</summary>
    [Required, MaxLength(1000)]
    public string OuDn { get; set; } = "";

    /// <summary>Danh sách DistinguishedName các group.</summary>
    public List<string> GroupDns { get; set; } = new();

    /// <summary>Rule thử việc: sau <see cref="ProbationDays"/> ngày sẽ cảnh báo chuyển rule.</summary>
    public bool IsProbation { get; set; }

    [Range(1, 3650)]
    public int ProbationDays { get; set; } = 7;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<UserRuleAssignment> Assignments { get; set; } = new();
}

/// <summary>
/// Ghi nhận tài khoản AD đang thuộc rule nào và từ thời điểm nào (dùng để tính hạn thử việc).
/// </summary>
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
