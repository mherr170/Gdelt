namespace GdeltSearchUI;

internal static class DebtPostTracker
{
    private static readonly string _filePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "GdeltAutoPost", "posted_debt.txt");

    private static readonly string _tsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "GdeltAutoPost", "debt_last_post_ts.txt");

    private static readonly HashSet<string> _posted = PostTrackerStore.Load(_filePath);

    public static bool HasBeenPosted(string recordDate) => _posted.Contains(recordDate);

    public static bool IsTodayPosted()
    {
        var cutoff = DateTime.Today.AddDays(-7);
        return _posted.Any(p => DateTime.TryParse(p, out var d) && d >= cutoff);
    }

    public static DateTime? GetLastPostedAt()
    {
        try
        {
            if (!File.Exists(_tsPath)) return null;
            var text = File.ReadAllText(_tsPath).Trim();
            return DateTime.TryParse(text, out var dt) ? dt : null;
        }
        catch { return null; }
    }

    public static void MarkPosted(string recordDate)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_tsPath)!);
            File.WriteAllText(_tsPath, DateTime.Now.ToString("O"));
        }
        catch { }

        _posted.Add(recordDate);
        PostTrackerStore.Append(_filePath, recordDate);
    }
}
