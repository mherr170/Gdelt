namespace GdeltSearchUI;

internal static class WeatherPostTracker
{
    private static readonly string _filePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "GdeltAutoPost", "posted_weather.txt");

    private static readonly HashSet<string> _posted = PostTrackerStore.Load(_filePath);

    // Keyed by NWS alert ID (full URL string)
    public static bool HasBeenPosted(string alertId) => _posted.Contains(alertId);

    public static void MarkPosted(string alertId)
    {
        _posted.Add(alertId);
        PostTrackerStore.Append(_filePath, alertId);
    }
}
