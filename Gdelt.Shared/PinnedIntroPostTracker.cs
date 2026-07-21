namespace GdeltSearchUI;

// Tracks which accounts already have a "Live Wire" intro post created, keyed by
// roster label, and remembers that post's AT-URI/CID. This lets a rerun retry
// pinning (cheap, idempotent — it's just a profile overwrite) without ever
// recreating the post itself.
internal static class PinnedIntroPostTracker
{
    private static readonly string _filePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "GdeltAutoPost", "posted_intro_pins.txt");

    private static readonly Dictionary<string, (string Uri, string Cid)> _posted = Load();
    private static readonly object _lock = new();

    public static (string Uri, string Cid)? GetPosted(string label)
    {
        lock (_lock) return _posted.TryGetValue(label, out var v) ? v : null;
    }

    public static void MarkPosted(string label, string uri, string cid)
    {
        lock (_lock) _posted[label] = (uri, cid);
        PostTrackerStore.Append(_filePath, $"{label}|{uri}|{cid}");
    }

    private static Dictionary<string, (string, string)> Load()
    {
        var dict = new Dictionary<string, (string, string)>();
        foreach (var line in PostTrackerStore.Load(_filePath))
        {
            var parts = line.Split('|', 3);
            if (parts.Length == 3) dict[parts[0]] = (parts[1], parts[2]);
        }
        return dict;
    }
}
