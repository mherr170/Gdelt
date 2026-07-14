using System.Globalization;

namespace GdeltSearchUI;

internal static class BlueskyFollowTracker
{
    private static readonly string _dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "GdeltAutoPost");

    private static readonly Dictionary<string, HashSet<string>> _cache = [];
    private static readonly object _lock = new();

    public static bool HasFollowed(string slug, string did)
    {
        lock (_lock) return GetSet(slug).Contains(did);
    }

    public static void MarkFollowed(string slug, string did)
    {
        lock (_lock) GetSet(slug).Add(did);
        PostTrackerStore.Append(FilePath(slug), did);
        // Also write a dated entry so the unfollow worker can check grace periods.
        PostTrackerStore.Append(DatedFilePath(slug), $"{did}\t{DateTime.UtcNow:O}");
    }

    // Removes a DID's grace-period entry once it's been resolved one way or the
    // other (unfollowed, or confirmed reciprocal past the grace period) so
    // GetFollowedBefore doesn't keep re-surfacing it — and the unfollow worker
    // doesn't keep re-fetching followers/follow-records for it — forever.
    public static void MarkResolved(string slug, string did)
    {
        var path = DatedFilePath(slug);
        if (!File.Exists(path)) return;
        lock (_lock)
        {
            var kept = File.ReadAllLines(path)
                .Where(line =>
                {
                    var tab = line.IndexOf('\t');
                    var lineDid = tab < 0 ? line : line[..tab];
                    return !lineDid.Equals(did, StringComparison.OrdinalIgnoreCase);
                })
                .ToArray();
            File.WriteAllLines(path, kept);
        }
    }

    // Returns DIDs that were followed before cutoffUtc and haven't yet been unfollowed.
    public static IReadOnlyList<string> GetFollowedBefore(string slug, DateTime cutoffUtc)
    {
        var path = DatedFilePath(slug);
        if (!File.Exists(path)) return [];

        var result = new List<string>();
        foreach (var line in File.ReadAllLines(path))
        {
            var tab = line.IndexOf('\t');
            if (tab < 0) continue;
            var did = line[..tab];
            var ts  = line[(tab + 1)..];
            if (DateTime.TryParse(ts, null, DateTimeStyles.RoundtripKind, out var dt) && dt < cutoffUtc)
                result.Add(did);
        }
        return result;
    }

    private static HashSet<string> GetSet(string slug)
    {
        if (_cache.TryGetValue(slug, out var set)) return set;
        set = new HashSet<string>(PostTrackerStore.Load(FilePath(slug)), StringComparer.OrdinalIgnoreCase);
        _cache[slug] = set;
        return set;
    }

    private static string FilePath(string slug)      => Path.Combine(_dir, $"follows_{slug}.txt");
    private static string DatedFilePath(string slug) => Path.Combine(_dir, $"follows_{slug}_dated.txt");
}
