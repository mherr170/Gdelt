namespace GdeltSearchUI;

internal static class YahooPostTracker
{
    private static readonly string _filePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "GdeltAutoPost", "posted_yahoo_futures.txt");

    private static readonly string _tsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "GdeltAutoPost", "yahoo_futures_last_post_ts.txt");

    private static readonly HashSet<string> _posted = PostTrackerStore.Load(_filePath);

    public static bool HasBeenPosted(string date) => _posted.Contains(date);

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
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_tsPath)!);
            File.WriteAllText(_tsPath, DateTime.Now.ToString("O"));
        }
        catch { }

        _posted.Add(date);
        PostTrackerStore.Append(_filePath, date);
    }
}
