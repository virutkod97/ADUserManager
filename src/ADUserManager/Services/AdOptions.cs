namespace ADUserManager.Services;

public class AdOptions
{
    /// <summary>
    /// Tên domain hoặc DC (vd: corp.local). Để trống = domain của máy chủ đang chạy service.
    /// </summary>
    public string? Domain { get; set; }

    /// <summary>
    /// Tài khoản dùng để thao tác AD. Để trống = dùng danh tính của service
    /// (LocalSystem trên DC, hoặc tài khoản/gMSA được cấu hình ở "Log On" của service).
    /// </summary>
    public string? Username { get; set; }
    public string? Password { get; set; }

    /// <summary>
    /// Các group (SID, sAMAccountName hoặc DN) được phép đăng nhập phần mềm.
    /// Mặc định: BUILTIN\Administrators (S-1-5-32-544) – Domain Admins/Enterprise Admins là thành viên của group này.
    /// </summary>
    public List<string> AdminGroups { get; set; } = new();

    /// <summary>Số lượng tối đa tài khoản trả về mỗi lần tìm kiếm.</summary>
    public int MaxSearchResults { get; set; } = 1000;

    /// <summary>Chỉ dùng khi phát triển (ASPNETCORE_ENVIRONMENT=Development): dữ liệu AD giả lập.</summary>
    public bool UseMock { get; set; }

    public IReadOnlyList<string> EffectiveAdminGroups =>
        AdminGroups.Count > 0 ? AdminGroups : new[] { "S-1-5-32-544" };
}
