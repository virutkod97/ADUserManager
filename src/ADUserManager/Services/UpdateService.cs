using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace ADUserManager.Services;

public class UpdateOptions
{
    public bool Enabled { get; set; } = true;
    public string Repository { get; set; } = "virutkod97/ADUserManager";
    public int CheckIntervalHours { get; set; } = 6;
    public string ApiBaseUrl { get; set; } = "https://api.github.com";
}

public record ReleaseInfo(
    Version Version, string Tag, string Name, string? Notes, string HtmlUrl, DateTime? PublishedAt,
    string? ZipUrl, string? ZipName, string? ChecksumUrl);

public class UpdateService
{
    private const string AssetSuffix = "-win-x64.zip";

    private readonly UpdateOptions _opt;
    private readonly IHttpClientFactory _http;
    private readonly IHostEnvironment _env;
    private readonly ILogger<UpdateService> _log;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public UpdateService(IOptions<UpdateOptions> opt, IHttpClientFactory http, IHostEnvironment env, ILogger<UpdateService> log)
    {
        _opt = opt.Value;
        _http = http;
        _env = env;
        _log = log;
        var v = Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0, 0);
        CurrentVersion = new Version(v.Major, v.Minor, Math.Max(v.Build, 0));
    }

    public Version CurrentVersion { get; }
    public string Repository => _opt.Repository;
    public bool Enabled => _opt.Enabled;
    public ReleaseInfo? Latest { get; private set; }
    public DateTime? LastCheckedUtc { get; private set; }
    public string? LastError { get; private set; }
    public bool IsUpdating { get; private set; }

    public bool UpdateAvailable => Latest is not null && Latest.Version > CurrentVersion;

    public string UpdateDir => Path.Combine(_env.ContentRootPath, "data", "update");
    public string LogFile => Path.Combine(UpdateDir, "update.log");

    public bool CanApply =>
        OperatingSystem.IsWindows() && UpdateAvailable && !IsUpdating
        && Latest!.ZipUrl is not null && Latest.ChecksumUrl is not null;

    private HttpClient Client()
    {
        var c = _http.CreateClient("github");
        c.Timeout = TimeSpan.FromMinutes(5);
        c.DefaultRequestHeaders.UserAgent.ParseAdd($"ADUserManager/{CurrentVersion}");
        c.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return c;
    }

    public async Task CheckAsync(CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            LastCheckedUtc = DateTime.UtcNow;
            using var res = await Client().GetAsync($"{_opt.ApiBaseUrl.TrimEnd('/')}/repos/{_opt.Repository}/releases/latest", ct);
            if (res.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                Latest = null;
                LastError = $"Không tìm thấy release nào ở {_opt.Repository} (repo chưa public hoặc chưa có release).";
                return;
            }
            res.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
            Latest = Parse(doc.RootElement);
            LastError = Latest is null ? "Không đọc được số phiên bản từ tag của release." : null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            LastError = "Không kiểm tra được cập nhật: " + ex.Message;
            _log.LogWarning(ex, "Update check failed");
        }
        finally
        {
            _lock.Release();
        }
    }

    private static ReleaseInfo? Parse(JsonElement r)
    {
        var tag = r.GetProperty("tag_name").GetString() ?? "";
        if (!Version.TryParse(tag.TrimStart('v', 'V'), out var ver)) return null;

        string? zipUrl = null, zipName = null, sumUrl = null;
        foreach (var a in r.GetProperty("assets").EnumerateArray())
        {
            var name = a.GetProperty("name").GetString() ?? "";
            var url = a.GetProperty("browser_download_url").GetString();
            if (name.EndsWith(AssetSuffix, StringComparison.OrdinalIgnoreCase)) { zipName = name; zipUrl = url; }
            else if (name.EndsWith(AssetSuffix + ".sha256", StringComparison.OrdinalIgnoreCase)) sumUrl = url;
        }

        DateTime? published = r.TryGetProperty("published_at", out var p) && p.ValueKind == JsonValueKind.String
            ? p.GetDateTime().ToUniversalTime()
            : null;

        return new ReleaseInfo(
            new Version(ver.Major, ver.Minor, Math.Max(ver.Build, 0)), tag,
            r.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString()! : tag,
            r.TryGetProperty("body", out var b) && b.ValueKind == JsonValueKind.String ? b.GetString() : null,
            r.GetProperty("html_url").GetString() ?? "",
            published, zipUrl, zipName, sumUrl);
    }

    // install-service.ps1 chạy ở tiến trình riêng: nó dừng chính service này, chép đè file rồi khởi động lại.
    public async Task ApplyAsync(CancellationToken ct = default)
    {
        if (!OperatingSystem.IsWindows()) throw new InvalidOperationException("Chỉ cập nhật được khi chạy trên Windows.");
        if (!CanApply) throw new InvalidOperationException("Không có bản cập nhật hợp lệ (thiếu gói .zip hoặc file .sha256 trong release).");

        await _lock.WaitAsync(ct);
        IsUpdating = true;
        try
        {
            var rel = Latest!;
            Directory.CreateDirectory(UpdateDir);
            var zipPath = Path.Combine(UpdateDir, rel.ZipName!);
            var http = Client();

            var expected = (await http.GetStringAsync(rel.ChecksumUrl, ct)).Trim().Split(' ', '\t', '\n')[0].ToLowerInvariant();
            await using (var src = await http.GetStreamAsync(rel.ZipUrl, ct))
            await using (var dst = File.Create(zipPath))
                await src.CopyToAsync(dst, ct);

            string actual;
            await using (var fs = File.OpenRead(zipPath))
                actual = Convert.ToHexString(await SHA256.HashDataAsync(fs, ct)).ToLowerInvariant();
            if (actual != expected)
            {
                File.Delete(zipPath);
                throw new InvalidOperationException("Checksum SHA-256 của gói tải về không khớp, đã huỷ cập nhật.");
            }

            var extractDir = Path.Combine(UpdateDir, rel.Tag);
            if (Directory.Exists(extractDir)) Directory.Delete(extractDir, recursive: true);
            ZipFile.ExtractToDirectory(zipPath, extractDir);

            var exe = Directory.GetFiles(extractDir, "ADUserManager.exe", SearchOption.AllDirectories).FirstOrDefault()
                      ?? throw new InvalidOperationException("Gói cập nhật không chứa ADUserManager.exe.");
            var packageDir = Path.GetDirectoryName(exe)!;
            var script = Path.Combine(packageDir, "install-service.ps1");
            if (!File.Exists(script)) throw new InvalidOperationException("Gói cập nhật không chứa install-service.ps1.");

            var psi = new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe"),
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = packageDir,
            };
            foreach (var a in new[]
                     {
                         "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", script,
                         "-SourceDir", packageDir,
                         "-InstallDir", AppContext.BaseDirectory.TrimEnd('\\'),
                         "-Upgrade", "-LogFile", LogFile,
                     })
                psi.ArgumentList.Add(a);

            _log.LogWarning("Starting update to {Version}", rel.Tag);
            Process.Start(psi);
        }
        catch
        {
            IsUpdating = false;
            throw;
        }
        finally
        {
            _lock.Release();
        }
    }

    public string? ReadLogTail(int maxLines = 60)
    {
        try
        {
            if (!File.Exists(LogFile)) return null;
            using var fs = new FileStream(LogFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var sr = new StreamReader(fs);
            var lines = sr.ReadToEnd().Split('\n');
            return string.Join('\n', lines.TakeLast(maxLines)).Trim();
        }
        catch
        {
            return null;
        }
    }
}

public class UpdateCheckWorker : BackgroundService
{
    private readonly UpdateService _updates;
    private readonly UpdateOptions _opt;

    public UpdateCheckWorker(UpdateService updates, IOptions<UpdateOptions> opt)
    {
        _updates = updates;
        _opt = opt.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_opt.Enabled) return;
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        var interval = TimeSpan.FromHours(Math.Max(1, _opt.CheckIntervalHours));
        while (!stoppingToken.IsCancellationRequested)
        {
            await _updates.CheckAsync(stoppingToken);
            await Task.Delay(interval, stoppingToken);
        }
    }
}
