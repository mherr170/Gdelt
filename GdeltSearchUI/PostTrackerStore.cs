namespace GdeltSearchUI;

/// <summary>Shared file-backed persistence for all per-feature post trackers.</summary>
internal static class PostTrackerStore
{
    public static HashSet<string> Load(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
                return [.. File.ReadAllLines(filePath).Where(l => l.Length > 0)];
        }
        catch (Exception ex)
        {
            AppLogger.Log($"PostTrackerStore load failed ({Path.GetFileName(filePath)}): {ex.Message}");
        }
        return [];
    }

    public static void Append(string filePath, string value)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            File.AppendAllLines(filePath, [value]);
        }
        catch (Exception ex)
        {
            AppLogger.Log($"PostTrackerStore save failed ({Path.GetFileName(filePath)}): {ex.Message}");
        }
    }
}
