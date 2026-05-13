namespace GdeltSearchUI;

internal static class QuakePostTracker
{
    private static readonly string _filePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "GdeltAutoPost", "posted_quakes.txt");

    private static readonly HashSet<string> _posted = PostTrackerStore.Load(_filePath);
    private static readonly object _lock = new();

    public static bool HasBeenPosted(string id) { lock (_lock) return _posted.Contains(id); }

    public static DateTime? GetLastPostedAt()
    {
        try { return File.Exists(_filePath) ? File.GetLastWriteTime(_filePath) : null; }
        catch { return null; }
    }

    public static void MarkPosted(string id)
    {
        lock (_lock) _posted.Add(id);
        PostTrackerStore.Append(_filePath, id);
    }
}
