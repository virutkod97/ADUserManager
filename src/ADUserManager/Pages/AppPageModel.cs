using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ADUserManager.Pages;

public abstract class AppPageModel : PageModel
{
    protected void FlashSuccess(string message) => TempData["Success"] = message;
    protected void FlashError(string message) => TempData["Error"] = message;

    protected void FlashWarnings(IEnumerable<string> warnings)
    {
        var text = string.Join('\n', warnings);
        if (!string.IsNullOrEmpty(text)) TempData["Warnings"] = text;
    }
}
