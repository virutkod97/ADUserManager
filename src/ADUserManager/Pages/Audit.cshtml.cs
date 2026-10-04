using ADUserManager.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ADUserManager.Pages;

public class AuditModel : AppPageModel
{
    public const int PageSize = 500;
    private readonly AppDbContext _db;
    public AuditModel(AppDbContext db) => _db = db;

    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    public List<AuditLog> Logs { get; private set; } = new();

    public async Task OnGetAsync()
    {
        IQueryable<AuditLog> q = _db.AuditLogs.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(Q))
        {
            var s = Q.Trim();
            q = q.Where(l => EF.Functions.Like(l.Actor, $"%{s}%") || EF.Functions.Like(l.Action, $"%{s}%")
                             || EF.Functions.Like(l.Target!, $"%{s}%") || EF.Functions.Like(l.Details!, $"%{s}%"));
        }
        Logs = await q.OrderByDescending(l => l.Id).Take(PageSize).ToListAsync();
    }
}
