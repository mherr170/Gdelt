namespace GdeltSearchUI;

internal static class ApodPostTracker
{
    private static readonly string _filePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "GdeltAutoPost", "posted_apod.txt");

    private static readonly HashSet<string> _posted = PostTrackerStore.Load(_filePath);
    private static readonly object _lock = new();

    public static bool HasBeenPosted(string date) { lock (_lock) return _posted.Contains(date); }

    public static void MarkPosted(string date)
    {
        lock (_lock) _posted.Add(date);
        PostTrackerStore.Append(_filePath, date);
    }
}
