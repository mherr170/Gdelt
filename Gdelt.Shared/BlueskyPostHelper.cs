namespace GdeltSearchUI;

internal static class BlueskyPostHelper
{
    public const string Divider = "━━━━━━━━━━━━━━";

    public static string HashtagLine(string[] tags) =>
        tags.Length > 0 ? "\n\n" + string.Join(" ", tags.Select(t => $"#{t}")) : "";

    // Delta helpers — produce the emoji + value string used in post bodies.
    // DeltaTextAbsolute: dollar difference (Gas Prices style, e.g. "📈 +0.042")
    public static string DeltaTextAbsolute(double? curr, double? prev)
    {
        if (!curr.HasValue || !prev.HasValue) return "";
        var d = curr.Value - prev.Value;
        if (Math.Abs(d) < 0.0005) return "➖ 0.000";
        var icon = d > 0 ? "📈" : "📉";
        return $"{icon} {(d > 0 ? "+" : "-")}{Math.Abs(d):F3}";
    }

    // DeltaTextPercent: % change relative to previous (Commodity style, e.g. "📉 -1.23%")
    public static string DeltaTextPercent(double curr, double? prev)
    {
        if (!prev.HasValue || prev.Value == 0) return "";
        var pct = (curr - prev.Value) / prev.Value * 100.0;
        if (Math.Abs(pct) < 0.005) return "➖";
        var icon = pct > 0 ? "📈" : "📉";
        return $"{icon} {(pct > 0 ? "+" : "")}{pct:F2}%";
    }

    // Converts ASCII letters and digits to Unicode mathematical bold (serif) —
    // the only reliable way to render bold in Bluesky posts.
    // Digits: U+1D7CE–U+1D7D7. Uppercase: U+1D400–U+1D419. Lowercase: U+1D41A–U+1D433.
    public static string Bold(string s)
    {
        var sb = new System.Text.StringBuilder(s.Length * 2);
        foreach (var c in s)
        {
            if (c is >= '0' and <= '9')      sb.Append(char.ConvertFromUtf32(0x1D7CE + (c - '0')));
            else if (c is >= 'A' and <= 'Z') sb.Append(char.ConvertFromUtf32(0x1D400 + (c - 'A')));
            else if (c is >= 'a' and <= 'z') sb.Append(char.ConvertFromUtf32(0x1D41A + (c - 'a')));
            else                              sb.Append(c);
        }
        return sb.ToString();
    }
}
