namespace ADUserManager.Services;

public static class DateExtensions
{
    /// <summary>SQLite trả về DateTime Kind=Unspecified; mọi thời điểm trong DB đều lưu dạng UTC.</summary>
    public static DateTime AsUtc(this DateTime d) =>
        d.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(d, DateTimeKind.Utc) : d.ToUniversalTime();

    public static string ToLocalDisplay(this DateTime d) => d.AsUtc().ToLocalTime().ToString("dd/MM/yyyy HH:mm");

    public static string ToLocalDisplay(this DateTime? d) => d.HasValue ? d.Value.ToLocalDisplay() : "—";

    public static string ToLocalDate(this DateTime d) => d.AsUtc().ToLocalTime().ToString("dd/MM/yyyy");
}
