namespace ADUserManager.Services;

public class AdOptions
{
    public string? Domain { get; set; }

    public string? Username { get; set; }
    public string? Password { get; set; }

    public List<string> AdminGroups { get; set; } = new();

    public int MaxSearchResults { get; set; } = 1000;

    public bool UseMock { get; set; }

    public IReadOnlyList<string> EffectiveAdminGroups =>
        AdminGroups.Count > 0 ? AdminGroups : new[] { "S-1-5-32-544" };
}
