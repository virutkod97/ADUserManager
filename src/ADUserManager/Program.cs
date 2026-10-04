using ADUserManager.Data;
using ADUserManager.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.WebEncoders;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    // Service chạy với thư mục làm việc C:\Windows\System32
    ContentRootPath = WindowsServiceHelpers.IsWindowsService() ? AppContext.BaseDirectory : default,
});

builder.Host.UseWindowsService(o => o.ServiceName = "ADUserManager");

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

builder.Services.Configure<UpdateOptions>(builder.Configuration.GetSection("Update"));
builder.Services.AddHttpClient();
builder.Services.AddSingleton<UpdateService>();
builder.Services.AddHostedService<UpdateCheckWorker>();

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
builder.Services.Configure<WebEncoderOptions>(o => o.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All));
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
