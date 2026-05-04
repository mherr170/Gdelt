namespace GdeltSearchUI;

internal static class YahooPostTracker
{
    private static readonly string _filePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GdeltSearchUI", "posted_yahoo_futures.txt");

    // Stores the exact DateTime of the most recent successful post.
    private static readonly string _tsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GdeltSearchUI", "yahoo_futures_last_post_ts.txt");

    private static readonly HashSet<string> _posted = PostTrackerStore.Load(_filePath);

    public static bool HasBeenPosted(string date) => _posted.Contains(date);

    /// <summary>Returns the DateTime of the last successful post, or null if never posted.</summary>
    public static DateTime? GetLastPostedAt()
    {
        try
        {
            if (!File.Exists(_tsPath)) return null;
            var text = File.ReadAllText(_tsPath).Trim();
            return DateTime.TryParse(text, out var dt) ? dt : null;
        }
        catch { return null; }
    }

    public static void MarkPosted(string date)
    {
        // Always update the timestamp so the 8-hour guard stays accurate
        // even when posting multiple times on the same calendar day.
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_tsPath)!);
            File.WriteAllText(_tsPath, DateTime.Now.ToString("O"));
        }
        catch { }

        if (!_posted.Add(date)) return;
        PostTrackerStore.Append(_filePath, date);
    }
}
