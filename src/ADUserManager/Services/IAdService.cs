namespace ADUserManager.Services;

public interface IAdService
{
    string DomainName { get; }

    AuthResult AuthenticateAdmin(string username, string password);

    IReadOnlyList<AdOu> GetOrganizationalUnits();
    IReadOnlyList<AdGroup> GetGroups();
    IReadOnlyList<string> GetUpnSuffixes();

    IReadOnlyList<AdUser> SearchUsers(string? query, string? ouDn = null);
    AdUser? GetUser(string samAccountName);
    bool UserExists(string samAccountName);

    AdUser CreateUser(NewUserRequest request);
    void UpdateUser(string samAccountName, UpdateUserRequest request);
    void DeleteUser(string samAccountName);

    void ResetPassword(string samAccountName, string newPassword, bool mustChange, bool unlock);
    void SetEnabled(string samAccountName, bool enabled);
    void Unlock(string samAccountName);

    void MoveUser(string samAccountName, string targetOuDn);
    void AddToGroup(string samAccountName, string groupDn);
    void RemoveFromGroup(string samAccountName, string groupDn);
}
