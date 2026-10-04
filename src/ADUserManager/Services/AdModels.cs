namespace ADUserManager.Services;

public class AdUser
{
    public string SamAccountName { get; set; } = "";
    public string DistinguishedName { get; set; } = "";
    public string? DisplayName { get; set; }
    public string? GivenName { get; set; }
    public string? Surname { get; set; }
    public string? UserPrincipalName { get; set; }
    public string? Email { get; set; }
    public string? Description { get; set; }
    public string? Department { get; set; }
    public string? Title { get; set; }
    public string? Phone { get; set; }
    public string? EmployeeId { get; set; }
    public bool Enabled { get; set; }
    public bool LockedOut { get; set; }
    public bool PasswordNeverExpires { get; set; }
    public DateTime? WhenCreated { get; set; }
    public DateTime? LastLogon { get; set; }
    public DateTime? PasswordLastSet { get; set; }
    public List<string> MemberOf { get; set; } = new();

    public string ParentDn => DnHelper.Parent(DistinguishedName);

    public AdUser Clone()
    {
        var c = (AdUser)MemberwiseClone();
        c.MemberOf = MemberOf.ToList();
        return c;
    }
}

public record AdOu(string DistinguishedName, string Path);

public record AdGroup(string DistinguishedName, string Name, string? Description);

public class NewUserRequest
{
    public string SamAccountName { get; set; } = "";
    public string UserPrincipalName { get; set; } = "";
    public string? GivenName { get; set; }
    public string? Surname { get; set; }
    public string DisplayName { get; set; } = "";
    public string? Email { get; set; }
    public string? Description { get; set; }
    public string? Department { get; set; }
    public string? Title { get; set; }
    public string? Phone { get; set; }
    public string? EmployeeId { get; set; }
    public string Password { get; set; } = "";
    public bool MustChangePassword { get; set; } = true;
    public bool Enabled { get; set; } = true;
    public string OuDn { get; set; } = "";
}

public class UpdateUserRequest
{
    public string? GivenName { get; set; }
    public string? Surname { get; set; }
    public string? DisplayName { get; set; }
    public string? UserPrincipalName { get; set; }
    public string? Email { get; set; }
    public string? Description { get; set; }
    public string? Department { get; set; }
    public string? Title { get; set; }
    public string? Phone { get; set; }
    public string? EmployeeId { get; set; }
    public bool PasswordNeverExpires { get; set; }
}

public enum AuthStatus { Success, InvalidCredentials, NotAuthorized, Error }

public record AuthResult(AuthStatus Status, string? SamAccountName = null, string? DisplayName = null, string? Error = null)
{
    public bool Success => Status == AuthStatus.Success;
}

public class AdOperationException : Exception
{
    public AdOperationException(string message, Exception? inner = null) : base(message, inner) { }
}
