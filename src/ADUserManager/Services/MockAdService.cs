namespace ADUserManager.Services;

public sealed class MockAdService : IAdService
{
    private const string Nc = "DC=corp,DC=local";
    private const string DomainUsersDn = "CN=Domain Users,CN=Users,DC=corp,DC=local";
    private readonly object _lock = new();
    private readonly Dictionary<string, (AdUser User, string Password)> _users = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<AdOu> _ous;
    private readonly List<AdGroup> _groups;

    public MockAdService()
    {
        _ous = new[]
        {
            $"CN=Users,{Nc}", $"OU=NPC,{Nc}", $"OU=Thu viec,OU=NPC,{Nc}", $"OU=Phong IT,OU=NPC,{Nc}",
            $"OU=Phong Ke toan,OU=NPC,{Nc}", $"OU=Phong Kinh doanh,OU=NPC,{Nc}", $"OU=Quan tri,OU=NPC,{Nc}",
        }.Select(d => new AdOu(d, DnHelper.ToPath(d))).OrderBy(o => o.Path).ToList();

        _groups = new (string Name, string Desc)[]
        {
            ("Domain Admins", "Quản trị domain"), ("GRP_ThuViec", "Nhân sự thử việc"), ("GRP_Internet", "Truy cập Internet"),
            ("GRP_IT", "Phòng IT"), ("GRP_KeToan", "Phòng Kế toán"), ("GRP_KinhDoanh", "Phòng Kinh doanh"),
            ("GRP_VPN", "Truy cập VPN"), ("GRP_FileServer_RW", "Ghi file server"),
            ("GRP_TBP", "Trưởng bộ phận"), ("GRP_PhapChe", "Pháp chế"),
            ("baocaotbp", "Báo cáo TBP"), ("TKhop", "Tài khoản họp"),
        }.Select(g => new AdGroup($"CN={g.Name},OU=NPC,{Nc}", g.Name, g.Desc)).ToList();
        _groups.Insert(0, new AdGroup(DomainUsersDn, "Domain Users", "Tất cả người dùng"));
        _groups.Add(new AdGroup($"CN=DL_MayIn,OU=NPC,{Nc}", "DL_MayIn", "Domain Local - máy in", CanBePrimary: false));

        Seed("admin", "Quản trị viên", "Admin@123", $"CN=Users,{Nc}", "Domain Admins");
        Seed("Administrator", "Administrator", "Admin@123", $"CN=Users,{Nc}", "Domain Admins");
        Seed("user01", "Người dùng thường", "User@123", $"CN=Users,{Nc}");
        Seed("anhnd", "Nguyễn Đức Anh", "P@ssw0rd!", $"OU=Phong IT,OU=NPC,{Nc}", "GRP_IT", "GRP_Internet");
        Seed("hoalt", "Lê Thị Hoa", "P@ssw0rd!", $"OU=Thu viec,OU=NPC,{Nc}", "GRP_ThuViec");
        Seed("hop", "Phòng Họp", "P@ssw0rd!", $"OU=NPC,{Nc}", "baocaotbp", "GRP_Internet");
        Seed("hop01", "Phòng Họp 01", "P@ssw0rd!", $"OU=NPC,{Nc}", "TKhop");
        _users["hop01"].User.PrimaryGroupDn = _groups.First(g => g.Name == "baocaotbp").DistinguishedName;
        Seed("disabled01", "Nhân viên Nghỉ việc", "P@ssw0rd!", $"OU=NPC,{Nc}");
        _users["disabled01"].User.Enabled = false;
        Seed("binhtv", "Trần Văn Bình", "P@ssw0rd!", $"OU=Phong Ke toan,OU=NPC,{Nc}", "GRP_KeToan");
    }

    private void Seed(string sam, string name, string pw, string ou, params string[] groups)
    {
        var parts = name.Split(' ');
        var u = new AdUser
        {
            SamAccountName = sam,
            DisplayName = name,
            GivenName = parts[^1],
            Surname = string.Join(' ', parts[..^1]),
            DistinguishedName = $"CN={name},{ou}",
            UserPrincipalName = $"{sam}@corp.local",
            Email = $"{sam}@corp.local",
            Enabled = true,
            WhenCreated = DateTime.UtcNow.AddDays(-30),
            PasswordLastSet = DateTime.UtcNow.AddDays(-10),
            MemberOf = groups.Select(g => _groups.First(x => x.Name == g).DistinguishedName).ToList(),
            PrimaryGroupDn = DomainUsersDn,
            LastLogon = sam == "user01" ? null : DateTime.UtcNow.AddHours(-(sam.Length * 7)),
        };
        _users[sam] = (u, pw);
    }

    private (AdUser User, string Password) Get(string sam) =>
        ProtectedAccounts.IsProtectedName(sam) ? throw new AdOperationException(ProtectedAccounts.Message)
        : _users.TryGetValue(sam, out var v) ? v : throw new AdOperationException($"Không tìm thấy tài khoản '{sam}'.");

    private static void CheckPassword(string pw)
    {
        int classes = new[] { pw.Any(char.IsUpper), pw.Any(char.IsLower), pw.Any(char.IsDigit), pw.Any(c => !char.IsLetterOrDigit(c)) }
            .Count(x => x);
        if (pw.Length < 8 || classes < 3)
            throw new AdOperationException("Mật khẩu không đáp ứng chính sách mật khẩu của domain (độ dài, độ phức tạp, lịch sử mật khẩu).");
    }

    public string DomainName => "corp.local";

    public AuthResult AuthenticateAdmin(string username, string password)
    {
        lock (_lock)
        {
            var sam = username.Contains('\\') ? username.Split('\\')[1] : username.Split('@')[0];
            if (!_users.TryGetValue(sam, out var v) || v.Password != password || !v.User.Enabled)
                return new AuthResult(AuthStatus.InvalidCredentials);
            v.User.LastLogon = DateTime.UtcNow;
            if (!v.User.MemberOf.Any(g => g.StartsWith("CN=Domain Admins,")))
                return new AuthResult(AuthStatus.NotAuthorized, sam);
            return new AuthResult(AuthStatus.Success, v.User.SamAccountName, v.User.DisplayName);
        }
    }

    public IReadOnlyList<AdOu> GetOrganizationalUnits() => _ous;
    public IReadOnlyList<AdGroup> GetGroups() => _groups;
    public IReadOnlyList<string> GetUpnSuffixes() => new[] { "corp.local", "npc.com.vn" };

    public IReadOnlyList<AdUser> SearchUsers(string? query, string? ouDn = null)
    {
        lock (_lock)
        {
            return _users.Values.Select(v => v.User)
                .Where(u => !ProtectedAccounts.IsProtectedName(u.SamAccountName))
                .Where(u => string.IsNullOrWhiteSpace(query)
                            || u.SamAccountName.Contains(query, StringComparison.OrdinalIgnoreCase)
                            || (u.DisplayName ?? "").Contains(query, StringComparison.OrdinalIgnoreCase)
                            || (u.Email ?? "").Contains(query, StringComparison.OrdinalIgnoreCase))
                .Where(u => string.IsNullOrEmpty(ouDn) || u.DistinguishedName.EndsWith("," + ouDn, StringComparison.OrdinalIgnoreCase))
                .OrderBy(u => u.SamAccountName)
                .Select(u => u.Clone())
                .ToList();
        }
    }

    public AdUser? GetUser(string samAccountName)
    {
        lock (_lock)
        {
            if (!_users.TryGetValue(samAccountName, out var v)) return null;
            if (ProtectedAccounts.IsProtectedName(samAccountName)) throw new AdOperationException(ProtectedAccounts.Message);
            var u = v.User.Clone();
            if (u.PrimaryGroupDn is not null && !u.MemberOf.Contains(u.PrimaryGroupDn)) u.MemberOf.Add(u.PrimaryGroupDn);
            return u;
        }
    }

    public bool UserExists(string samAccountName)
    {
        lock (_lock) return _users.ContainsKey(samAccountName);
    }

    public AdUser CreateUser(NewUserRequest r)
    {
        lock (_lock)
        {
            if (_users.ContainsKey(r.SamAccountName)) throw new AdOperationException($"Tài khoản '{r.SamAccountName}' đã tồn tại.");
            CheckPassword(r.Password);
            var u = new AdUser
            {
                SamAccountName = r.SamAccountName,
                DistinguishedName = $"CN={DnHelper.EscapeRdnValue(r.DisplayName)},{r.OuDn}",
                DisplayName = r.DisplayName,
                GivenName = r.GivenName,
                Surname = r.Surname,
                UserPrincipalName = r.UserPrincipalName,
                Email = r.Email,
                Description = r.Description,
                Department = r.Department,
                Title = r.Title,
                Phone = r.Phone,
                EmployeeId = r.EmployeeId,
                Enabled = r.Enabled,
                WhenCreated = DateTime.UtcNow,
                PasswordLastSet = r.MustChangePassword ? null : DateTime.UtcNow,
                PrimaryGroupDn = DomainUsersDn,
            };
            _users[u.SamAccountName] = (u, r.Password);
            return u.Clone();
        }
    }

    public void UpdateUser(string sam, UpdateUserRequest r)
    {
        lock (_lock)
        {
            var u = Get(sam).User;
            u.GivenName = r.GivenName; u.Surname = r.Surname;
            if (!string.IsNullOrWhiteSpace(r.DisplayName)) u.DisplayName = r.DisplayName;
            if (!string.IsNullOrWhiteSpace(r.UserPrincipalName)) u.UserPrincipalName = r.UserPrincipalName;
            u.Email = r.Email; u.Description = r.Description; u.Department = r.Department;
            u.Title = r.Title; u.Phone = r.Phone; u.EmployeeId = r.EmployeeId;
            u.PasswordNeverExpires = r.PasswordNeverExpires;
        }
    }

    public void DeleteUser(string sam)
    {
        lock (_lock)
        {
            Get(sam);
            _users.Remove(sam);
        }
    }

    public void ResetPassword(string sam, string pw, bool mustChange, bool unlock)
    {
        lock (_lock)
        {
            var v = Get(sam);
            CheckPassword(pw);
            v.User.PasswordLastSet = mustChange ? null : DateTime.UtcNow;
            if (unlock) v.User.LockedOut = false;
            _users[sam] = (v.User, pw);
        }
    }

    public void SetEnabled(string sam, bool enabled)
    {
        lock (_lock) Get(sam).User.Enabled = enabled;
    }

    public void Unlock(string sam)
    {
        lock (_lock) Get(sam).User.LockedOut = false;
    }

    public void MoveUser(string sam, string targetOuDn)
    {
        lock (_lock)
        {
            var u = Get(sam).User;
            u.DistinguishedName = DnHelper.Split(u.DistinguishedName)[0] + "," + targetOuDn;
        }
    }

    public IReadOnlyList<string> GetUserGroups(string sam)
    {
        lock (_lock)
        {
            var u = Get(sam).User;
            return u.MemberOf.Append(u.PrimaryGroupDn!).Where(g => g is not null).Distinct().ToList();
        }
    }

    public string GetDomainUsersDn() => DomainUsersDn;

    // Giống AD: phải là thành viên mới đặt làm primary; group primary cũ trở thành thành viên thường
    public void SetPrimaryGroup(string sam, string groupDn)
    {
        lock (_lock)
        {
            var u = Get(sam).User;
            if (string.Equals(u.PrimaryGroupDn, groupDn, StringComparison.OrdinalIgnoreCase)) return;
            var g = _groups.FirstOrDefault(x => string.Equals(x.DistinguishedName, groupDn, StringComparison.OrdinalIgnoreCase))
                    ?? throw new AdOperationException($"Không tìm thấy group '{DnHelper.ToPath(groupDn)}'.");
            if (!g.CanBePrimary) throw new AdOperationException($"Group '{g.Name}' không dùng được làm primary group (chỉ Global/Universal).");
            if (!u.MemberOf.Contains(groupDn, StringComparer.OrdinalIgnoreCase))
                throw new AdOperationException("The server is unwilling to process the request. (tài khoản chưa là thành viên của group)");
            u.MemberOf.RemoveAll(x => string.Equals(x, groupDn, StringComparison.OrdinalIgnoreCase));
            if (u.PrimaryGroupDn is not null) u.MemberOf.Add(u.PrimaryGroupDn);
            u.PrimaryGroupDn = groupDn;
        }
    }

    public void AddToGroup(string sam, string groupDn)
    {
        lock (_lock)
        {
            if (_groups.All(g => g.DistinguishedName != groupDn))
                throw new AdOperationException($"Không tìm thấy group '{DnHelper.ToPath(groupDn)}'.");
            var u = Get(sam).User;
            if (string.Equals(u.PrimaryGroupDn, groupDn, StringComparison.OrdinalIgnoreCase)) return;
            if (!u.MemberOf.Contains(groupDn, StringComparer.OrdinalIgnoreCase)) u.MemberOf.Add(groupDn);
        }
    }

    public void RemoveFromGroup(string sam, string groupDn)
    {
        lock (_lock)
        {
            var u = Get(sam).User;
            if (string.Equals(u.PrimaryGroupDn, groupDn, StringComparison.OrdinalIgnoreCase))
                throw new AdOperationException($"Không gỡ được '{sam}' khỏi group '{DnHelper.RdnValue(DnHelper.Split(groupDn)[0])}': The server is unwilling to process the request. (đang là primary group)");
            u.MemberOf.RemoveAll(g => string.Equals(g, groupDn, StringComparison.OrdinalIgnoreCase));
        }
    }
}
