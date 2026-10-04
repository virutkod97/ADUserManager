using ADUserManager.Data;

namespace ADUserManager.Services;

public class AuditService
{
    private readonly AppDbContext _db;
    private readonly IHttpContextAccessor _http;
    private readonly ILogger<AuditService> _log;

    public AuditService(AppDbContext db, IHttpContextAccessor http, ILogger<AuditService> log)
    {
        _db = db;
        _http = http;
        _log = log;
    }

    public string CurrentActor => _http.HttpContext?.User.Identity?.Name ?? "system";

    public async Task LogAsync(string action, string? target, string? details = null, bool success = true, string? actor = null)
    {
        try
        {
            _db.AuditLogs.Add(new AuditLog
            {
                Actor = actor ?? CurrentActor,
                Action = action,
                Target = target,
                Details = details,
                Success = success,
                ClientIp = _http.HttpContext?.Connection.RemoteIpAddress?.ToString(),
            });
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Cannot write audit log {Action} {Target}", action, target);
        }
    }
}
