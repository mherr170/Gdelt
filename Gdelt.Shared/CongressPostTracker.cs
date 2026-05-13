namespace GdeltSearchUI;

internal static class CongressPostTracker
{
    private static readonly string _filePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "GdeltAutoPost", "posted_congress.txt");

    private static readonly HashSet<string> _posted = PostTrackerStore.Load(_filePath);
    private static readonly object _lock = new();

    public static bool HasBeenPosted(string uniqueKey) { lock (_lock) return _posted.Contains(uniqueKey); }

    public static void MarkPosted(string uniqueKey)
    {
        lock (_lock) _posted.Add(uniqueKey);
        PostTrackerStore.Append(_filePath, uniqueKey);
    }
}
