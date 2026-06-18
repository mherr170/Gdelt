namespace GdeltSearchUI;

// Tracks posted slots as "{date}:{slot}" keys, e.g. "2026-06-18:morning"
internal static class BirdPostTracker
{
    private static readonly string _filePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "GdeltAutoPost", "posted_bird.txt");

    private static readonly string _tsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "GdeltAutoPost", "bird_last_post_ts.txt");

    private static readonly HashSet<string> _posted = PostTrackerStore.Load(_filePath);
    private static readonly object _lock = new();

    public static bool HasBeenPosted(string slotKey) { lock (_lock) return _posted.Contains(slotKey); }

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

    public static void MarkPosted(string slotKey)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_tsPath)!);
            File.WriteAllText(_tsPath, DateTime.Now.ToString("O"));
        }
        catch { }

        lock (_lock) _posted.Add(slotKey);
        PostTrackerStore.Append(_filePath, slotKey);
    }
}
