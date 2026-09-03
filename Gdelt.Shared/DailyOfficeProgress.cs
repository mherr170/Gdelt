using System.Text.Json;

namespace GdeltSearchUI;

// Cursor for "The Daily Office" lesson crawl. Two continuous read-throughs run
// in parallel — one OT (Genesis→Malachi, Psalms skipped), one NT
// (Matthew→Revelation) — each advancing one chapter per office. Both wrap
// around at the end and never "complete".
internal sealed class DailyOfficeProgress
{
    public int OtBook    { get; set; }          // index into DailyOfficeData.OldTestament
    public int OtChapter { get; set; } = 1;
    public int NtBook    { get; set; }          // index into DailyOfficeData.NewTestament
    public int NtChapter { get; set; } = 1;

    // "yyyy-MM-dd" of the last successful post for each office — the idempotency
    // guard so a restart within the same day cannot double-post.
    public string LastMorningSlot { get; set; } = "";
    public string LastEveningSlot { get; set; } = "";
}

internal static class DailyOfficeProgressStore
{
    private static readonly string _filePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "GdeltAutoPost", "daily_office_progress.json");

    private static readonly object _lock = new();
    private static readonly JsonSerializerOptions _json = new() { WriteIndented = true };

    public static DailyOfficeProgress Load()
    {
        lock (_lock)
        {
            try
            {
                if (File.Exists(_filePath))
                    return JsonSerializer.Deserialize<DailyOfficeProgress>(File.ReadAllText(_filePath), _json)
                           ?? new DailyOfficeProgress();
            }
            catch (Exception ex)
            {
                AppLogger.Log($"DailyOfficeProgressStore load failed: {ex.Message}");
            }
            return new DailyOfficeProgress();
        }
    }

    public static void Save(DailyOfficeProgress p)
    {
        lock (_lock)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
                var tmp = _filePath + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(p, _json));
                File.Move(tmp, _filePath, overwrite: true);
            }
            catch (Exception ex)
            {
                AppLogger.Log($"DailyOfficeProgressStore save failed: {ex.Message}");
            }
        }
    }
}
