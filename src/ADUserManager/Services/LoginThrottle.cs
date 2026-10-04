using System.Collections.Concurrent;

namespace ADUserManager.Services;

/// <summary>Chặn tạm thời khi đăng nhập sai nhiều lần (theo IP + tên đăng nhập).</summary>
public class LoginThrottle
{
    private const int MaxFailures = 5;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan Block = TimeSpan.FromMinutes(5);

    private readonly ConcurrentDictionary<string, (int Count, DateTime First, DateTime? BlockedUntil)> _state = new();

    public static string Key(string? ip, string user) => $"{ip}|{user.Trim().ToLowerInvariant()}";

    public bool IsBlocked(string key, out TimeSpan remaining)
    {
        remaining = TimeSpan.Zero;
        if (_state.TryGetValue(key, out var s) && s.BlockedUntil is { } until && until > DateTime.UtcNow)
        {
            remaining = until - DateTime.UtcNow;
            return true;
        }
        return false;
    }

    public void Fail(string key)
    {
        var now = DateTime.UtcNow;
        _state.AddOrUpdate(key,
            _ => (1, now, null),
            (_, s) =>
            {
                if (now - s.First > Window || (s.BlockedUntil is { } b && b <= now)) s = (0, now, null);
                var count = s.Count + 1;
                return (count, s.First, count >= MaxFailures ? now + Block : null);
            });
    }

    public void Success(string key) => _state.TryRemove(key, out _);
}
