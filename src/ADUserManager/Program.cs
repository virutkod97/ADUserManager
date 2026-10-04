using ADUserManager.Data;
using ADUserManager.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting.WindowsServices;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    // Khi chạy dạng Windows Service, thư mục làm việc là C:\Windows\System32 → dùng thư mục chứa exe.
    ContentRootPath = WindowsServiceHelpers.IsWindowsService() ? AppContext.BaseDirectory : default,
});

builder.Host.UseWindowsService(o => o.ServiceName = "ADUserManager");

// ---------------------------------------------------------------- data
var dataDir = Path.Combine(builder.Environment.ContentRootPath, "data");
Directory.CreateDirectory(dataDir);

var connectionString = builder.Configuration.GetConnectionString("Default");
if (string.IsNullOrWhiteSpace(connectionString))
    connectionString = $"Data Source={Path.Combine(dataDir, "adusermanager.db")}";
builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlite(connectionString));

var dp = builder.Services.AddDataProtection()
    .SetApplicationName("ADUserManager")
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(dataDir, "keys")));
if (OperatingSystem.IsWindows()) dp.ProtectKeysWithDpapi(protectToLocalMachine: true);

// ---------------------------------------------------------------- Active Directory
builder.Services.Configure<AdOptions>(builder.Configuration.GetSection("ActiveDirectory"));
var adOptions = builder.Configuration.GetSection("ActiveDirectory").Get<AdOptions>() ?? new AdOptions();
if (adOptions.UseMock && builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton<IAdService, MockAdService>();
}
else if (OperatingSystem.IsWindows())
{
    builder.Services.AddSingleton<IAdService, AdService>();
}
else
{
    throw new PlatformNotSupportedException(
        "ADUserManager chỉ chạy trên Windows (máy chủ AD). Để chạy thử trên nền tảng khác, đặt ASPNETCORE_ENVIRONMENT=Development và ActiveDirectory:UseMock=true.");
}

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<RuleService>();
builder.Services.AddSingleton<LoginThrottle>();

// ---------------------------------------------------------------- auth
var sessionMinutes = builder.Configuration.GetValue("App:SessionTimeoutMinutes", 30);
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/Login";
        o.LogoutPath = "/Logout";
        o.AccessDeniedPath = "/Login";
        o.ExpireTimeSpan = TimeSpan.FromMinutes(sessionMinutes);
        o.SlidingExpiration = true;
        o.Cookie.Name = "ADUM.Auth";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Strict;
        o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    });

builder.Services.AddAuthorization(o =>
{
    o.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .RequireRole(AppRoles.Admin)
        .Build();
});

builder.Services.AddRazorPages(o =>
{
    o.Conventions.AllowAnonymousToPage("/Login");
    o.Conventions.AllowAnonymousToPage("/Error");
});
builder.Services.AddAntiforgery(o =>
{
    o.Cookie.Name = "ADUM.AF";
    o.Cookie.SameSite = SameSiteMode.Strict;
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.Use(async (ctx, next) =>
{
    var h = ctx.Response.Headers;
    h["X-Frame-Options"] = "DENY";
    h["X-Content-Type-Options"] = "nosniff";
    h["Referrer-Policy"] = "no-referrer";
    h["Content-Security-Policy"] = "default-src 'self'; img-src 'self' data:; style-src 'self'; script-src 'self'; frame-ancestors 'none'";
    h["Cache-Control"] = "no-store";
    await next();
});

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();

app.Run();

public static class AppRoles
{
    public const string Admin = "ADUM.Admin";
}
