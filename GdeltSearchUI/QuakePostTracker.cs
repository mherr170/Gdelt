namespace GdeltSearchUI;

internal static class QuakePostTracker
{
    private static readonly string _filePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GdeltSearchUI", "posted_quakes.txt");

    private static readonly HashSet<string> _posted = PostTrackerStore.Load(_filePath);

    public static bool HasBeenPosted(string id) => _posted.Contains(id);

    public static void MarkPosted(string id)
    {
        if (!_posted.Add(id)) return;
        PostTrackerStore.Append(_filePath, id);
    }
}
