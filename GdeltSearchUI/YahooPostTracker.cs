namespace GdeltSearchUI;

internal static class YahooPostTracker
{
    private static readonly string _filePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GdeltSearchUI", "posted_yahoo_futures.txt");

    private static readonly HashSet<string> _posted = PostTrackerStore.Load(_filePath);

    public static bool HasBeenPosted(string date) => _posted.Contains(date);

    public static void MarkPosted(string date)
    {
        if (!_posted.Add(date)) return;
        PostTrackerStore.Append(_filePath, date);
    }
}
