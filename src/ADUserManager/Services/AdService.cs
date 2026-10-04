using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Extensions.Options;

namespace ADUserManager.Services;

[SupportedOSPlatform("windows")]
public sealed class AdService : IAdService
{
    private const AuthenticationTypes AuthTypes =
        AuthenticationTypes.Secure | AuthenticationTypes.Signing | AuthenticationTypes.Sealing;

    private const ContextOptions CtxOptions =
        ContextOptions.Negotiate | ContextOptions.Signing | ContextOptions.Sealing;

    private static readonly string[] UserProps =
    {
        "sAMAccountName", "distinguishedName", "displayName", "givenName", "sn", "userPrincipalName",
        "mail", "description", "department", "title", "telephoneNumber", "employeeID",
        "userAccountControl", "lockoutTime", "whenCreated", "lastLogonTimestamp", "pwdLastSet", "memberOf",
    };

    private readonly AdOptions _opt;
    private readonly ILogger<AdService> _log;
    private readonly Lazy<(string DefaultNc, string ConfigNc)> _rootDse;

    public AdService(IOptions<AdOptions> options, ILogger<AdService> log)
    {
        _opt = options.Value;
        _log = log;
        _rootDse = new Lazy<(string, string)>(() =>
        {
            using var rootDse = Entry("RootDSE");
            var def = rootDse.Properties["defaultNamingContext"].Value as string
                      ?? throw new AdOperationException("Không đọc được defaultNamingContext từ RootDSE.");
            var cfg = rootDse.Properties["configurationNamingContext"].Value as string ?? "";
            return (def, cfg);
        });
    }

    private string DefaultNc => _rootDse.Value.DefaultNc;
    private string? DomainOrNull => string.IsNullOrWhiteSpace(_opt.Domain) ? null : _opt.Domain.Trim();
    private bool HasCredentials => !string.IsNullOrWhiteSpace(_opt.Username);

    public string DomainName => DnHelper.DomainFromDn(DefaultNc);

    private string LdapPath(string dn) =>
        "LDAP://" + (DomainOrNull is null ? "" : DomainOrNull + "/") + dn.Replace("/", "\\/");

    private DirectoryEntry Entry(string dn)
    {
        var path = LdapPath(dn);
        return HasCredentials
            ? new DirectoryEntry(path, _opt.Username, _opt.Password, AuthTypes)
            : new DirectoryEntry(path, null, null, AuthTypes);
    }

    private PrincipalContext Ctx(string? container = null) =>
        HasCredentials
            ? new PrincipalContext(ContextType.Domain, DomainOrNull, container, CtxOptions, _opt.Username, _opt.Password)
            : new PrincipalContext(ContextType.Domain, DomainOrNull, container, CtxOptions);

    private static UserPrincipal FindUser(PrincipalContext ctx, string sam) =>
        UserPrincipal.FindByIdentity(ctx, IdentityType.SamAccountName, sam)
        ?? throw new AdOperationException($"Không tìm thấy tài khoản '{sam}'.");

    private static string? N(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    private static void SetAttr(DirectoryEntry de, string name, string? value)
    {
        value = N(value);
        if (value is null)
        {
            if (de.Properties.Contains(name)) de.Properties[name].Clear();
        }
        else de.Properties[name].Value = value;
    }

    private T Run<T>(string operation, Func<T> action)
    {
        try
        {
            return action();
        }
        catch (AdOperationException) { throw; }
        catch (PasswordException ex)
        {
            throw new AdOperationException(
                "Mật khẩu không đáp ứng chính sách mật khẩu của domain (độ dài, độ phức tạp, lịch sử mật khẩu).", ex);
        }
        catch (PrincipalExistsException ex)
        {
            throw new AdOperationException("Đối tượng đã tồn tại trong Active Directory.", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new AdOperationException("Tài khoản chạy service không đủ quyền thực hiện thao tác này.", ex);
        }
        catch (DirectoryServicesCOMException ex)
        {
            _log.LogError(ex, "AD error during {Operation}", operation);
            var msg = string.IsNullOrWhiteSpace(ex.ExtendedErrorMessage) ? ex.Message : $"{ex.Message} ({ex.ExtendedErrorMessage})";
            throw new AdOperationException($"Lỗi Active Directory khi {operation}: {msg}", ex);
        }
        catch (Exception ex) when (ex is COMException or PrincipalOperationException or PrincipalServerDownException or InvalidOperationException)
        {
            _log.LogError(ex, "AD error during {Operation}", operation);
            throw new AdOperationException($"Lỗi Active Directory khi {operation}: {ex.Message}", ex);
        }
    }

    private void Run(string operation, Action action) => Run<object?>(operation, () => { action(); return null; });

    public AuthResult AuthenticateAdmin(string username, string password)
    {
        try
        {
            using var ctx = Ctx();
            using var user = UserPrincipal.FindByIdentity(ctx, username.Trim());
            if (user is null || string.IsNullOrEmpty(user.SamAccountName))
                return new AuthResult(AuthStatus.InvalidCredentials);

            if (!ctx.ValidateCredentials(user.SamAccountName, password, ContextOptions.Negotiate))
                return new AuthResult(AuthStatus.InvalidCredentials);

            if (!IsAdmin(ctx, user))
                return new AuthResult(AuthStatus.NotAuthorized, user.SamAccountName);

            return new AuthResult(AuthStatus.Success, user.SamAccountName, user.DisplayName ?? user.Name);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Login failed for {User}", username);
            return new AuthResult(AuthStatus.Error, Error: ex.Message);
        }
    }

    private bool IsAdmin(PrincipalContext ctx, UserPrincipal user)
    {
        // tokenGroups đã đệ quy và gồm cả group BUILTIN khi truy vấn trên DC
        var tokenSids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var g in user.GetAuthorizationGroups())
            {
                using (g)
                {
                    if (g.Sid is not null) tokenSids.Add(g.Sid.Value);
                }
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "GetAuthorizationGroups failed for {User}", user.SamAccountName);
        }

        foreach (var id in _opt.EffectiveAdminGroups)
        {
            try
            {
                using var grp = GroupPrincipal.FindByIdentity(ctx, id);
                if (grp?.Sid is null)
                {
                    _log.LogWarning("Admin group {Group} not found", id);
                    continue;
                }
                if (tokenSids.Contains(grp.Sid.Value)) return true;

                foreach (var m in grp.GetMembers(recursive: true))
                {
                    using (m)
                    {
                        if (m.Sid is not null && m.Sid.Equals(user.Sid)) return true;
                    }
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Cannot check membership of {Group}", id);
            }
        }
        return false;
    }

    public IReadOnlyList<AdOu> GetOrganizationalUnits() => Run("đọc danh sách OU", () =>
    {
        using var root = Entry(DefaultNc);
        using var s = new DirectorySearcher(root, "(objectCategory=organizationalUnit)", new[] { "distinguishedName" })
        {
            PageSize = 1000,
            SearchScope = SearchScope.Subtree,
        };
        using var results = s.FindAll();
        var list = new List<AdOu> { new($"CN=Users,{DefaultNc}", DnHelper.ToPath($"CN=Users,{DefaultNc}")) };
        foreach (SearchResult r in results)
        {
            var dn = Str(r, "distinguishedName");
            if (dn is not null) list.Add(new AdOu(dn, DnHelper.ToPath(dn)));
        }
        return (IReadOnlyList<AdOu>)list.OrderBy(o => o.Path, StringComparer.CurrentCultureIgnoreCase).ToList();
    });

    public IReadOnlyList<AdGroup> GetGroups() => Run("đọc danh sách group", () =>
    {
        using var root = Entry(DefaultNc);
        using var s = new DirectorySearcher(root, "(objectCategory=group)", new[] { "distinguishedName", "cn", "description" })
        {
            PageSize = 1000,
            SearchScope = SearchScope.Subtree,
        };
        using var results = s.FindAll();
        var list = new List<AdGroup>();
        foreach (SearchResult r in results)
        {
            var dn = Str(r, "distinguishedName");
            if (dn is not null) list.Add(new AdGroup(dn, Str(r, "cn") ?? dn, Str(r, "description")));
        }
        return (IReadOnlyList<AdGroup>)list.OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    });

    public IReadOnlyList<string> GetUpnSuffixes()
    {
        var list = new List<string> { DomainName };
        try
        {
            if (!string.IsNullOrEmpty(_rootDse.Value.ConfigNc))
            {
                using var partitions = Entry($"CN=Partitions,{_rootDse.Value.ConfigNc}");
                foreach (var v in partitions.Properties["uPNSuffixes"])
                    if (v is string sfx && !list.Contains(sfx, StringComparer.OrdinalIgnoreCase)) list.Add(sfx);
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Cannot read UPN suffixes");
        }
        return list;
    }

    public IReadOnlyList<AdUser> SearchUsers(string? query, string? ouDn = null) => Run("tìm kiếm tài khoản", () =>
    {
        var filter = "(&(objectCategory=person)(objectClass=user)";
        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = DnHelper.EscapeFilter(query.Trim());
            filter += $"(|(sAMAccountName=*{q}*)(displayName=*{q}*)(mail=*{q}*)(userPrincipalName=*{q}*)(employeeID={q}))";
        }
        filter += ")";

        using var root = Entry(string.IsNullOrWhiteSpace(ouDn) ? DefaultNc : ouDn);
        using var s = new DirectorySearcher(root, filter, UserProps)
        {
            PageSize = 500,
            SizeLimit = _opt.MaxSearchResults,
            SearchScope = SearchScope.Subtree,
        };
        using var results = s.FindAll();
        var list = new List<AdUser>();
        foreach (SearchResult r in results) list.Add(Map(r));
        return (IReadOnlyList<AdUser>)list.OrderBy(u => u.SamAccountName, StringComparer.OrdinalIgnoreCase).ToList();
    });

    private SearchResult? FindOne(string sam)
    {
        using var root = Entry(DefaultNc);
        using var s = new DirectorySearcher(root,
            $"(&(objectCategory=person)(objectClass=user)(sAMAccountName={DnHelper.EscapeFilter(sam)}))", UserProps)
        {
            SearchScope = SearchScope.Subtree,
        };
        return s.FindOne();
    }

    public AdUser? GetUser(string samAccountName) => Run("đọc thông tin tài khoản", () =>
    {
        var r = FindOne(samAccountName);
        if (r is null) return null;
        var u = Map(r);
        try
        {
            using var ctx = Ctx();
            using var up = UserPrincipal.FindByIdentity(ctx, IdentityType.SamAccountName, samAccountName);
            if (up is not null) u.LockedOut = up.IsAccountLockedOut();
        }
        catch (Exception ex)
        {
            _log.LogDebug(ex, "Cannot read lockout state");
        }
        return u;
    });

    public bool UserExists(string samAccountName) => Run("kiểm tra tài khoản", () => FindOne(samAccountName) is not null);

    public AdUser CreateUser(NewUserRequest req) => Run("tạo tài khoản", () =>
    {
        if (FindOne(req.SamAccountName) is not null)
            throw new AdOperationException($"Tài khoản '{req.SamAccountName}' đã tồn tại.");

        var cn = req.DisplayName.Trim();
        if (CnExists(req.OuDn, cn)) cn = $"{cn} ({req.SamAccountName})";

        using (var ctx = Ctx(req.OuDn))
        using (var up = new UserPrincipal(ctx))
        {
            up.SamAccountName = req.SamAccountName.Trim();
            up.UserPrincipalName = req.UserPrincipalName.Trim();
            up.Name = cn;
            up.DisplayName = req.DisplayName.Trim();
            up.GivenName = N(req.GivenName);
            up.Surname = N(req.Surname);
            up.EmailAddress = N(req.Email);
            up.Description = N(req.Description);
            up.EmployeeId = N(req.EmployeeId);
            up.VoiceTelephoneNumber = N(req.Phone);
            up.SetPassword(req.Password);
            up.Enabled = req.Enabled;

            try
            {
                up.Save();
            }
            catch
            {
                // Save() có thể tạo xong object rồi mới lỗi đặt mật khẩu
                TryDeleteOrphan(req.SamAccountName);
                throw;
            }

            if (req.MustChangePassword) up.ExpirePasswordNow();

            if (N(req.Department) is not null || N(req.Title) is not null)
            {
                var de = (DirectoryEntry)up.GetUnderlyingObject();
                SetAttr(de, "department", req.Department);
                SetAttr(de, "title", req.Title);
                de.CommitChanges();
            }
        }

        return GetUser(req.SamAccountName) ?? throw new AdOperationException("Đã tạo nhưng không đọc lại được tài khoản.");
    });

    private bool CnExists(string ouDn, string cn)
    {
        try
        {
            using var ou = Entry(ouDn);
            using var s = new DirectorySearcher(ou, $"(cn={DnHelper.EscapeFilter(cn)})", new[] { "cn" })
            {
                SearchScope = SearchScope.OneLevel,
            };
            return s.FindOne() is not null;
        }
        catch
        {
            return false;
        }
    }

    private void TryDeleteOrphan(string sam)
    {
        try
        {
            using var ctx = Ctx();
            using var up = UserPrincipal.FindByIdentity(ctx, IdentityType.SamAccountName, sam);
            up?.Delete();
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Could not clean up partially created user {Sam}", sam);
        }
    }

    public void UpdateUser(string samAccountName, UpdateUserRequest req) => Run("cập nhật tài khoản", () =>
    {
        using var ctx = Ctx();
        using var up = FindUser(ctx, samAccountName);
        up.GivenName = N(req.GivenName);
        up.Surname = N(req.Surname);
        if (N(req.DisplayName) is { } dn) up.DisplayName = dn;
        if (N(req.UserPrincipalName) is { } upn) up.UserPrincipalName = upn;
        up.EmailAddress = N(req.Email);
        up.Description = N(req.Description);
        up.EmployeeId = N(req.EmployeeId);
        up.VoiceTelephoneNumber = N(req.Phone);
        up.PasswordNeverExpires = req.PasswordNeverExpires;
        up.Save();

        var de = (DirectoryEntry)up.GetUnderlyingObject();
        SetAttr(de, "department", req.Department);
        SetAttr(de, "title", req.Title);
        de.CommitChanges();
    });

    public void DeleteUser(string samAccountName) => Run("xoá tài khoản", () =>
    {
        using var ctx = Ctx();
        using var up = FindUser(ctx, samAccountName);
        up.Delete();
    });

    public void ResetPassword(string samAccountName, string newPassword, bool mustChange, bool unlock) =>
        Run("đặt lại mật khẩu", () =>
        {
            using var ctx = Ctx();
            using var up = FindUser(ctx, samAccountName);
            up.SetPassword(newPassword);
            if (mustChange) up.ExpirePasswordNow();
            if (unlock && up.IsAccountLockedOut()) up.UnlockAccount();
        });

    public void SetEnabled(string samAccountName, bool enabled) => Run(enabled ? "kích hoạt tài khoản" : "vô hiệu hoá tài khoản", () =>
    {
        using var ctx = Ctx();
        using var up = FindUser(ctx, samAccountName);
        up.Enabled = enabled;
        up.Save();
    });

    public void Unlock(string samAccountName) => Run("mở khoá tài khoản", () =>
    {
        using var ctx = Ctx();
        using var up = FindUser(ctx, samAccountName);
        if (up.IsAccountLockedOut()) up.UnlockAccount();
    });

    public void MoveUser(string samAccountName, string targetOuDn) => Run("di chuyển tài khoản sang OU mới", () =>
    {
        using var ctx = Ctx();
        using var up = FindUser(ctx, samAccountName);
        if (string.Equals(DnHelper.Parent(up.DistinguishedName), targetOuDn, StringComparison.OrdinalIgnoreCase)) return;
        var de = (DirectoryEntry)up.GetUnderlyingObject();
        using var target = Entry(targetOuDn);
        de.MoveTo(target);
    });

    public void AddToGroup(string samAccountName, string groupDn) => Run("thêm vào group", () =>
        ChangeMembership(samAccountName, groupDn, add: true));

    public void RemoveFromGroup(string samAccountName, string groupDn) => Run("gỡ khỏi group", () =>
        ChangeMembership(samAccountName, groupDn, add: false));

    // Sửa trực tiếp thuộc tính member của group rồi đọc lại memberOf của tài khoản để xác nhận
    private void ChangeMembership(string sam, string groupDn, bool add)
    {
        var groupName = DnHelper.RdnValue(DnHelper.Split(groupDn)[0]);
        var (userDn, memberOf) = ReadMembership(sam);
        if (memberOf.Contains(groupDn) == add) return;

        using (var grp = Entry(groupDn))
        {
            try
            {
                _ = grp.NativeObject;
            }
            catch (COMException ex)
            {
                throw new AdOperationException($"Không mở được group '{groupName}': {ex.Message}", ex);
            }
            // IADsGroup.Add/Remove ghi thẳng lên AD, không phụ thuộc giới hạn 1500 giá trị khi đọc thuộc tính member
            try
            {
                grp.Invoke(add ? "Add" : "Remove", LdapPath(userDn));
            }
            catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is not null)
            {
                throw new AdOperationException(
                    $"Không {(add ? "thêm" : "gỡ")} được '{sam}' {(add ? "vào" : "khỏi")} group '{groupName}': {ex.InnerException.Message}", ex.InnerException);
            }
        }

        var (_, after) = ReadMembership(sam);
        if (after.Contains(groupDn) != add)
            throw new AdOperationException(add
                ? $"Đã gửi lệnh thêm '{sam}' vào group '{groupName}' nhưng kiểm tra lại vẫn chưa là thành viên."
                : $"Đã gửi lệnh gỡ '{sam}' khỏi group '{groupName}' nhưng kiểm tra lại vẫn còn là thành viên.");
    }

    private (string UserDn, HashSet<string> MemberOf) ReadMembership(string sam)
    {
        var r = FindOne(sam) ?? throw new AdOperationException($"Không tìm thấy tài khoản '{sam}'.");
        var dn = Str(r, "distinguishedName")!;
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // Đọc trực tiếp trên object (base scope) để không dính độ trễ của kết quả tìm kiếm
        using (var user = Entry(dn))
        {
            user.RefreshCache(new[] { "memberOf" });
            foreach (var g in user.Properties["memberOf"])
                if (g is string s) set.Add(s);
        }
        return (dn, set);
    }

    private static string? Str(SearchResult r, string prop) =>
        r.Properties.Contains(prop) && r.Properties[prop].Count > 0 ? r.Properties[prop][0]?.ToString() : null;

    private static long Long(SearchResult r, string prop) =>
        r.Properties.Contains(prop) && r.Properties[prop].Count > 0 && r.Properties[prop][0] is long l ? l : 0;

    private static DateTime? FileTime(long v) =>
        v > 0 && v != long.MaxValue ? DateTime.FromFileTimeUtc(v) : null;

    private static AdUser Map(SearchResult r)
    {
        int uac = r.Properties.Contains("userAccountControl") && r.Properties["userAccountControl"].Count > 0
            ? Convert.ToInt32(r.Properties["userAccountControl"][0])
            : 0;
        DateTime? created = r.Properties.Contains("whenCreated") && r.Properties["whenCreated"].Count > 0
                            && r.Properties["whenCreated"][0] is DateTime wc
            ? DateTime.SpecifyKind(wc, DateTimeKind.Utc)
            : null;

        var u = new AdUser
        {
            SamAccountName = Str(r, "sAMAccountName") ?? "",
            DistinguishedName = Str(r, "distinguishedName") ?? "",
            DisplayName = Str(r, "displayName"),
            GivenName = Str(r, "givenName"),
            Surname = Str(r, "sn"),
            UserPrincipalName = Str(r, "userPrincipalName"),
            Email = Str(r, "mail"),
            Description = Str(r, "description"),
            Department = Str(r, "department"),
            Title = Str(r, "title"),
            Phone = Str(r, "telephoneNumber"),
            EmployeeId = Str(r, "employeeID"),
            Enabled = (uac & 0x2) == 0,
            PasswordNeverExpires = (uac & 0x10000) != 0,
            LockedOut = Long(r, "lockoutTime") > 0,
            WhenCreated = created,
            LastLogon = FileTime(Long(r, "lastLogonTimestamp")),
            PasswordLastSet = FileTime(Long(r, "pwdLastSet")),
        };
        if (r.Properties.Contains("memberOf"))
            foreach (var g in r.Properties["memberOf"])
                if (g is string s) u.MemberOf.Add(s);
        return u;
    }
}
