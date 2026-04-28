namespace GdeltSearchUI;

internal static class DebtPostTracker
{
    private static readonly string _filePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GdeltSearchUI", "posted_debt.txt");

    private static readonly HashSet<string> _posted = PostTrackerStore.Load(_filePath);

    public static bool HasBeenPosted(string recordDate) => _posted.Contains(recordDate);

    public static bool IsTodayPosted()
    {
        // Treasury data lags 1 business day; weekends/holidays can stretch to 3-4 days.
        var cutoff = DateTime.Today.AddDays(-7);
        return _posted.Any(p => DateTime.TryParse(p, out var d) && d >= cutoff);
    }

    public static void MarkPosted(string recordDate)
    {
        if (!_posted.Add(recordDate)) return;
        PostTrackerStore.Append(_filePath, recordDate);
    }
}
