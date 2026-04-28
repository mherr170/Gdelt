namespace GdeltSearchUI;

internal static class GasPricePostTracker
{
    private static readonly string _filePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GdeltSearchUI", "posted_gasprices.txt");

    private static readonly HashSet<string> _posted = PostTrackerStore.Load(_filePath);

    public static bool HasBeenPosted(string period) => _posted.Contains(period);

    public static bool IsCurrentWeekPosted()
    {
        // EIA data lags a week and release timing varies; treat any record within
        // the last 7 days as "current week posted".
        var cutoff = DateTime.Today.AddDays(-7);
        return _posted.Any(p => DateTime.TryParse(p, out var d) && d >= cutoff);
    }

    public static void MarkPosted(string period)
    {
        if (!_posted.Add(period)) return;
        PostTrackerStore.Append(_filePath, period);
    }
}
