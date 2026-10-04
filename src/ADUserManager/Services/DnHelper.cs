using System.Text;

namespace ADUserManager.Services;

public static class DnHelper
{
    public static List<string> Split(string dn)
    {
        var parts = new List<string>();
        var sb = new StringBuilder();
        for (int i = 0; i < dn.Length; i++)
        {
            char c = dn[i];
            if (c == '\\' && i + 1 < dn.Length)
            {
                sb.Append(c).Append(dn[++i]);
            }
            else if (c == ',')
            {
                parts.Add(sb.ToString().Trim());
                sb.Clear();
            }
            else sb.Append(c);
        }
        if (sb.Length > 0) parts.Add(sb.ToString().Trim());
        return parts;
    }

    public static string Parent(string dn)
    {
        var parts = Split(dn);
        return parts.Count <= 1 ? "" : string.Join(",", parts.Skip(1));
    }

    public static string RdnValue(string rdn)
    {
        int idx = rdn.IndexOf('=');
        var v = idx >= 0 ? rdn[(idx + 1)..] : rdn;
        return Unescape(v);
    }

    public static string Unescape(string v)
    {
        if (!v.Contains('\\')) return v;
        var sb = new StringBuilder();
        for (int i = 0; i < v.Length; i++)
        {
            if (v[i] == '\\' && i + 1 < v.Length) sb.Append(v[++i]);
            else sb.Append(v[i]);
        }
        return sb.ToString();
    }

    public static string EscapeRdnValue(string v)
    {
        var sb = new StringBuilder();
        foreach (var c in v)
        {
            if (",+\"\\<>;=#".Contains(c)) sb.Append('\\');
            sb.Append(c);
        }
        return sb.ToString().Trim();
    }

    public static string DomainFromDn(string dn) =>
        string.Join(".", Split(dn)
            .Where(p => p.StartsWith("DC=", StringComparison.OrdinalIgnoreCase))
            .Select(RdnValue));

    public static string ToPath(string dn)
    {
        if (string.IsNullOrEmpty(dn)) return "";
        var parts = Split(dn);
        var domain = DomainFromDn(dn);
        var names = parts
            .Where(p => !p.StartsWith("DC=", StringComparison.OrdinalIgnoreCase))
            .Select(RdnValue)
            .Reverse();
        return string.Join("/", new[] { domain }.Concat(names));
    }

    public static string EscapeFilter(string v)
    {
        var sb = new StringBuilder();
        foreach (var c in v)
        {
            switch (c)
            {
                case '\\': sb.Append(@"\5c"); break;
                case '*': sb.Append(@"\2a"); break;
                case '(': sb.Append(@"\28"); break;
                case ')': sb.Append(@"\29"); break;
                case '\0': sb.Append(@"\00"); break;
                default: sb.Append(c); break;
            }
        }
        return sb.ToString();
    }
}
