namespace GdeltSearchUI;

internal static class BlueskyLikeTracker
{
    private static readonly string _dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "GdeltAutoPost");

    private static readonly Dictionary<string, HashSet<string>> _cache = [];
    private static readonly object _lock = new();

    public static bool HasLiked(string slug, string postUri)
    {
        lock (_lock) return GetSet(slug).Contains(postUri);
    }

    public static void MarkLiked(string slug, string postUri)
    {
        lock (_lock) GetSet(slug).Add(postUri);
        PostTrackerStore.Append(FilePath(slug), postUri);
    }

    private static HashSet<string> GetSet(string slug)
    {
        if (_cache.TryGetValue(slug, out var set)) return set;
        set = new HashSet<string>(PostTrackerStore.Load(FilePath(slug)), StringComparer.OrdinalIgnoreCase);
        _cache[slug] = set;
        return set;
    }

    private static string FilePath(string slug) =>
        Path.Combine(_dir, $"likes_{slug}.txt");
}
