using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace ADUserManager.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<AccountRule> Rules => Set<AccountRule>();
    public DbSet<UserRuleAssignment> Assignments => Set<UserRuleAssignment>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // DN không bao giờ chứa ký tự xuống dòng nên dùng '\n' làm dấu phân cách.
        var listComparer = new ValueComparer<List<string>>(
            (a, c) => a!.SequenceEqual(c!),
            v => v.Aggregate(0, (h, s) => HashCode.Combine(h, s.GetHashCode())),
            v => v.ToList());

        b.Entity<AccountRule>(e =>
        {
            e.HasIndex(x => x.Name).IsUnique();
            e.Property(x => x.Name).UseCollation("NOCASE");
            e.Property(x => x.GroupDns)
                .HasConversion(
                    v => string.Join('\n', v),
                    v => v.Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList())
                .Metadata.SetValueComparer(listComparer);
        });

        b.Entity<UserRuleAssignment>(e =>
        {
            e.Property(x => x.SamAccountName).UseCollation("NOCASE");
            e.HasIndex(x => x.SamAccountName).IsUnique();
            e.HasOne(x => x.Rule)
                .WithMany(r => r.Assignments)
                .HasForeignKey(x => x.RuleId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<AuditLog>(e =>
        {
            e.HasIndex(x => x.Time);
        });
    }
}
