namespace GdeltSearchUI;

internal static class QuakePostTracker
{
    private static readonly string _filePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GdeltSearchUI", "posted_quakes.txt");

    private static readonly HashSet<string> _posted = Load();

    public static bool HasBeenPosted(string id) => _posted.Contains(id);

    public static void MarkPosted(string id)
    {
        if (!_posted.Add(id)) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            File.AppendAllLines(_filePath, [id]);
        }
        catch (Exception ex)
        {
            AppLogger.Log($"QuakePostTracker save failed: {ex.Message}");
        }
    }

    private static HashSet<string> Load()
    {
        try
        {
            if (File.Exists(_filePath))
                return [.. File.ReadAllLines(_filePath).Where(l => l.Length > 0)];
        }
        catch (Exception ex)
        {
            AppLogger.Log($"QuakePostTracker load failed: {ex.Message}");
        }
        return [];
    }
}
