using ADUserManager.Services;
using Microsoft.AspNetCore.Mvc;

namespace ADUserManager.Pages;

public class UpdateModel : AppPageModel
{
    private readonly AuditService _audit;

    public UpdateModel(UpdateService updates, AuditService audit)
    {
        Updates = updates;
        _audit = audit;
    }

    public UpdateService Updates { get; }
    public string? Log { get; private set; }

    public void OnGet() => Log = Updates.ReadLogTail();

    public async Task<IActionResult> OnPostCheckAsync()
    {
        await Updates.CheckAsync(HttpContext.RequestAborted);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostApplyAsync()
    {
        var target = Updates.Latest?.Tag;
        try
        {
            await Updates.ApplyAsync(HttpContext.RequestAborted);
            await _audit.LogAsync("App.Update", target, $"v{Updates.CurrentVersion} → {target}");
            FlashSuccess($"Đang cập nhật lên {target}. Service sẽ khởi động lại, tải lại trang sau khoảng 1 phút.");
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or IOException)
        {
            await _audit.LogAsync("App.Update", target, ex.Message, success: false);
            FlashError(ex.Message);
        }
        return RedirectToPage();
    }
}
