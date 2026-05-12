namespace GdeltSearchUI;

internal static class GunViolencePostTracker
{
    private static readonly string _filePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "GdeltAutoPost", "posted_gunviolence.txt");

    private static readonly HashSet<string> _posted;
    private static readonly List<HashSet<string>> _postedWordSets;

    private static readonly HashSet<string> _stopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "the", "in", "at", "of", "and", "or", "is", "are", "was", "were",
        "for", "to", "on", "with", "after", "before", "that", "this", "by", "from",
        "as", "into", "about", "says", "amid", "over", "near", "following",
    };

    static GunViolencePostTracker()
    {
        _posted      = [];
        _postedWordSets = [];

        if (!File.Exists(_filePath)) return;

        foreach (var line in File.ReadAllLines(_filePath).Where(l => l.Length > 0))
        {
            var tab = line.IndexOf('\t');
            _posted.Add(tab >= 0 ? line[..tab] : line);
            if (tab >= 0)
                _postedWordSets.Add(WordSet(line[(tab + 1)..]));
        }
    }

    public static bool HasBeenPosted(string url) => _posted.Contains(url);

    public static DateTime? GetLastPostedAt()
    {
        try { return File.Exists(_filePath) ? File.GetLastWriteTime(_filePath) : null; }
        catch { return null; }
    }

    // Returns true if a title with significant word overlap has already been posted,
    // catching same-incident articles from different news sources across polling runs.
    public static bool HasSimilarIncidentBeenPosted(string title, double threshold = 0.30)
    {
        if (_postedWordSets.Count == 0) return false;
        var candidate = WordSet(title);
        if (candidate.Count == 0) return false;

        foreach (var stored in _postedWordSets)
        {
            var intersection = candidate.Count(w => stored.Contains(w));
            var union        = candidate.Count + stored.Count - intersection;
            if (union > 0 && (double)intersection / union >= threshold)
                return true;
        }
        return false;
    }

    public static void MarkPosted(string url, string title)
    {
        _posted.Add(url);
        _postedWordSets.Add(WordSet(title));
        PostTrackerStore.Append(_filePath, $"{url}\t{title}");
    }

    private static HashSet<string> WordSet(string title) =>
        new(title
            .ToLowerInvariant()
            .Split([' ', '-', ',', '.', '\'', '"', ':', ';', '!', '?', '(', ')', '|'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(w => w.Length >= 3 && !_stopWords.Contains(w)),
            StringComparer.OrdinalIgnoreCase);
}
