namespace GdeltSearchUI;

internal static class PostTrackerStore
{
    private const string MutexPrefix = "Global\\GdeltPostTracker_";

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
        // Named mutex keyed on the file so UI and service cannot race each other.
        var mutexName = MutexPrefix + Path.GetFileNameWithoutExtension(filePath);
        using var mutex = new Mutex(false, mutexName);
        bool acquired = false;
        try
        {
            acquired = mutex.WaitOne(TimeSpan.FromSeconds(5));
            if (!acquired)
            {
                AppLogger.Log($"PostTrackerStore mutex timeout ({Path.GetFileName(filePath)}) — skipping write");
                return;
            }

            // Re-read inside the lock — another process may have written this value
            // after we loaded our in-memory HashSet but before we got here.
            if (File.Exists(filePath) &&
                File.ReadAllLines(filePath).Any(l => l == value))
                return;

            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            File.AppendAllLines(filePath, [value]);
        }
        catch (Exception ex)
        {
            AppLogger.Log($"PostTrackerStore save failed ({Path.GetFileName(filePath)}): {ex.Message}");
        }
        finally
        {
            if (acquired) try { mutex.ReleaseMutex(); } catch { }
        }
    }
}
