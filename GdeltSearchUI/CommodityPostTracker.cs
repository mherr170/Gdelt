namespace GdeltSearchUI;

internal static class CommodityPostTracker
{
    private static readonly string _filePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GdeltSearchUI", "posted_commodities.txt");

    private static readonly HashSet<string> _posted = PostTrackerStore.Load(_filePath);

    public static bool HasBeenPosted(string date) => _posted.Contains(date);

    public static bool IsRecentlyPosted()
    {
        // Futures markets close on weekends; treat posted within 3 days as current.
        var cutoff = DateTime.Today.AddDays(-3);
        return _posted.Any(p => DateTime.TryParse(p, out var d) && d >= cutoff);
    }

    public static void MarkPosted(string date)
    {
        if (!_posted.Add(date)) return;
        PostTrackerStore.Append(_filePath, date);
    }
}
