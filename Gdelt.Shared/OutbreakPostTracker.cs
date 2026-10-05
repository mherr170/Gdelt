namespace GdeltSearchUI;

internal static class OutbreakPostTracker
{
    private static readonly string _filePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "GdeltAutoPost", "posted_outbreak.txt");

    // Shorter than gun violence: an outbreak is one long-running story, so each
    // genuine development (death, quarantine, official confirmation) needs to be able
    // to post again once the previous headline has aged out of the window.
    private static readonly TimeSpan SimilarityWindow = TimeSpan.FromDays(2);

    private static readonly HashSet<string> _posted;
    private static readonly List<(DateTime PostedAt, string Title, HashSet<string> Words)> _postedWordSets;
    private static readonly object _lock = new();

    private static readonly HashSet<string> _stopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "the", "in", "at", "of", "and", "or", "is", "are", "was", "were",
        "for", "to", "on", "with", "after", "before", "that", "this", "by", "from",
        "as", "into", "about", "says", "amid", "over", "near", "following",
    };

    static OutbreakPostTracker()
    {
        _posted         = [];
        _postedWordSets = [];

        if (!File.Exists(_filePath)) return;

        foreach (var line in File.ReadAllLines(_filePath).Where(l => l.Length > 0))
        {
            var fields = line.Split('\t');
            _posted.Add(fields[0]);

            if (fields.Length >= 3 && DateTime.TryParse(fields[2], null,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var postedAt))
                _postedWordSets.Add((postedAt, fields[1], WordSet(fields[1])));
        }
    }

    public static bool HasBeenPosted(string url) { lock (_lock) return _posted.Contains(url); }

    public static DateTime? GetLastPostedAt()
    {
        try { return File.Exists(_filePath) ? File.GetLastWriteTime(_filePath) : null; }
        catch { return null; }
    }

    // True if a title with significant word overlap was posted within the recency
    // window — catches the same story from different outlets across polling runs.
    public static bool HasSimilarStoryBeenPosted(string title, double threshold = 0.40)
    {
        lock (_lock)
        {
            if (_postedWordSets.Count == 0) return false;
            var candidate = WordSet(title);
            if (candidate.Count == 0) return false;

            var cutoff = DateTime.UtcNow - SimilarityWindow;

            foreach (var (postedAt, _, stored) in _postedWordSets)
            {
                if (postedAt < cutoff) continue;

                var intersection = candidate.Count(w => stored.Contains(w));
                var union        = candidate.Count + stored.Count - intersection;
                if (union > 0 && (double)intersection / union >= threshold)
                    return true;
            }
            return false;
        }
    }

    public static IReadOnlyList<string> GetRecentPostedTitles()
    {
        lock (_lock)
        {
            var cutoff = DateTime.UtcNow - SimilarityWindow;
            return _postedWordSets
                .Where(p => p.PostedAt >= cutoff)
                .Select(p => p.Title)
                .ToList();
        }
    }

    public static void MarkPosted(string url, string title)
    {
        var postedAt = DateTime.UtcNow;
        lock (_lock)
        {
            _posted.Add(url);
            _postedWordSets.Add((postedAt, title, WordSet(title)));
        }
        PostTrackerStore.Append(_filePath, $"{url}\t{title}\t{postedAt:O}");
    }

    private static HashSet<string> WordSet(string title) =>
        new(title
            .ToLowerInvariant()
            .Split([' ', '-', ',', '.', '\'', '"', ':', ';', '!', '?', '(', ')', '|'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(w => w.Length >= 3 && !_stopWords.Contains(w)),
            StringComparer.OrdinalIgnoreCase);
}
