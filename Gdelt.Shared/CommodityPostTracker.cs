namespace GdeltSearchUI;

internal static class CommodityPostTracker
{
    private static readonly string _filePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "GdeltAutoPost", "posted_commodities.txt");

    private static readonly HashSet<string> _posted = PostTrackerStore.Load(_filePath);

    public static bool HasBeenPosted(string date) => _posted.Contains(date);

    public static bool IsRecentlyPosted()
    {
        var cutoff = DateTime.Today.AddDays(-3);
        return _posted.Any(p => DateTime.TryParse(p, out var d) && d >= cutoff);
    }

    public static void MarkPosted(string date)
    {
        _posted.Add(date);
        PostTrackerStore.Append(_filePath, date);
    }
}
