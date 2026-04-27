namespace GdeltSearchUI;

internal static class BlueskyPostHelper
{
    public static string HashtagLine(string[] tags) =>
        tags.Length > 0 ? "\n\n" + string.Join(" ", tags.Select(t => $"#{t}")) : "";
}
