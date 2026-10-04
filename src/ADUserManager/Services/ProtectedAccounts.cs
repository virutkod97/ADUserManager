namespace ADUserManager.Services;

// Tài khoản nhạy cảm: không hiển thị và không cho thao tác qua phần mềm (vẫn đăng nhập được)
public static class ProtectedAccounts
{
    private static readonly HashSet<string> Names = new(StringComparer.OrdinalIgnoreCase) { "administrator" };

    public const string Message = "Tài khoản quản trị gốc (administrator) được bảo vệ, không thao tác qua phần mềm.";

    public static bool IsProtectedName(string? sam) => sam is not null && Names.Contains(sam.Trim());

    // RID 500 = tài khoản Administrator gốc của domain, kể cả khi đã đổi tên
    public static bool IsBuiltInAdminSid(string? sid) => sid is not null && sid.StartsWith("S-1-5-21-") && sid.EndsWith("-500");
}
