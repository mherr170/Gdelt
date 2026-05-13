namespace GdeltSearchUI;

internal static class StockPostTracker
{
    private static readonly string _filePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "GdeltAutoPost", "posted_stocks.txt");

    private static readonly HashSet<string> _posted = PostTrackerStore.Load(_filePath);
    private static readonly object _lock = new();

    // Keyed by Eastern-time trading date string: "yyyy-MM-dd"
    public static bool HasBeenPosted(string tradingDate) { lock (_lock) return _posted.Contains(tradingDate); }

    public static void MarkPosted(string tradingDate)
    {
        lock (_lock) _posted.Add(tradingDate);
        PostTrackerStore.Append(_filePath, tradingDate);
    }
}
