using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ADUserManager.Data;

// EnsureCreated không sửa DB đã có, nên các thay đổi schema sau bản 1.1 được áp dụng theo PRAGMA user_version.
public static class SchemaUpgrader
{
    private static readonly string[][] Steps =
    {
        new[]
        {
            "ALTER TABLE \"Rules\" ADD COLUMN \"Kind\" INTEGER NOT NULL DEFAULT 0",
            """
            CREATE TABLE IF NOT EXISTS "PermissionAssignments" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_PermissionAssignments" PRIMARY KEY AUTOINCREMENT,
                "SamAccountName" TEXT COLLATE NOCASE NOT NULL,
                "RuleId" INTEGER NOT NULL,
                "AssignedAt" TEXT NOT NULL,
                "AssignedBy" TEXT NULL,
                CONSTRAINT "FK_PermissionAssignments_Rules_RuleId" FOREIGN KEY ("RuleId") REFERENCES "Rules" ("Id") ON DELETE RESTRICT
            )
            """,
            "CREATE INDEX IF NOT EXISTS \"IX_PermissionAssignments_RuleId\" ON \"PermissionAssignments\" (\"RuleId\")",
            "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_PermissionAssignments_SamAccountName_RuleId\" ON \"PermissionAssignments\" (\"SamAccountName\", \"RuleId\")",
        },
    };

    public static int LatestVersion => Steps.Length;

    public static void Upgrade(AppDbContext db, ILogger log)
    {
        var created = db.Database.EnsureCreated();
        db.Database.OpenConnection();
        try
        {
            if (created)
            {
                SetVersion(db, LatestVersion);
                return;
            }

            var version = GetVersion(db);
            for (var i = version; i < Steps.Length; i++)
            {
                using var tx = db.Database.BeginTransaction();
                foreach (var sql in Steps[i]) db.Database.ExecuteSqlRaw(sql);
                SetVersion(db, i + 1);
                tx.Commit();
                log.LogInformation("Database schema upgraded to version {Version}", i + 1);
            }
        }
        finally
        {
            db.Database.CloseConnection();
        }
    }

    private static int GetVersion(AppDbContext db)
    {
        using var cmd = db.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = "PRAGMA user_version";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    private static void SetVersion(AppDbContext db, int version)
    {
        using var cmd = db.Database.GetDbConnection().CreateCommand();
        cmd.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        cmd.CommandText = "PRAGMA user_version = " + version.ToString(System.Globalization.CultureInfo.InvariantCulture);
        cmd.ExecuteNonQuery();
    }
}
