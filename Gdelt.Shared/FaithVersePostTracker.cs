namespace GdeltSearchUI;

// Tracks which calendar days the Daily Bible Verse account has already posted.
// The count of posted days doubles as the index into FaithVerseData.References
// for the next verse, so the rotation advances exactly one step per post and
// survives restarts.
internal static class FaithVersePostTracker
{
    private static readonly string _filePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "GdeltAutoPost", "posted_faithverse.txt");

    private static readonly HashSet<string> _posted = PostTrackerStore.Load(_filePath);
    private static readonly object _lock = new();

    public static bool HasBeenPosted(string dateKey) { lock (_lock) return _posted.Contains(dateKey); }

    // Days posted so far — also the next index into the verse rotation.
    public static int PostedCount { get { lock (_lock) return _posted.Count; } }

    public static void MarkPosted(string dateKey)
    {
        lock (_lock) _posted.Add(dateKey);
        PostTrackerStore.Append(_filePath, dateKey);
    }
}
