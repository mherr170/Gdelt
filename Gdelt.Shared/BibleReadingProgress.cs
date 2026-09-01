using System.Text.Json;
using System.Text.Json.Serialization;

namespace GdeltSearchUI;

// Persisted cursor for the sequential Bible crawl. The worker caches one whole
// chapter at a time and serves a single verse per hour from it, so a normal tick
// does no network I/O at all.
internal sealed class BibleReadingProgress
{
    // Position in BibleBooks.All and within the current book.
    public int BookIndex { get; set; }
    public int Chapter   { get; set; } = 1;

    // 0-based index of the next verse to post within CachedVerses.
    public int NextIndex { get; set; }

    // Verses successfully posted so far — drives the "N / 31,102" readout.
    public int Ordinal { get; set; }

    // The chapter currently held in the cache (0 = nothing cached yet).
    public string CachedBook    { get; set; } = "";
    public int    CachedChapter { get; set; }
    public List<CachedVerse> CachedVerses { get; set; } = [];

    // Hour slot ("yyyy-MM-ddTHH") of the last successful post — the idempotency
    // guard so a restart within the same hour cannot double-post.
    public string LastSlot { get; set; } = "";

    // Set once the final book/chapter/verse has posted. The worker then idles.
    public bool Complete { get; set; }
}

internal sealed record CachedVerse(
    [property: JsonPropertyName("v")] int    Verse,
    [property: JsonPropertyName("t")] string Text);

internal static class BibleProgressStore
{
    private static readonly string _filePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "GdeltAutoPost", "bible_progress.json");

    private static readonly object _lock = new();
    private static readonly JsonSerializerOptions _json = new() { WriteIndented = true };

    public static BibleReadingProgress Load()
    {
        lock (_lock)
        {
            try
            {
                if (File.Exists(_filePath))
                    return JsonSerializer.Deserialize<BibleReadingProgress>(File.ReadAllText(_filePath), _json)
                           ?? new BibleReadingProgress();
            }
            catch (Exception ex)
            {
                AppLogger.Log($"BibleProgressStore load failed: {ex.Message}");
            }
            return new BibleReadingProgress();
        }
    }

    public static void Save(BibleReadingProgress progress)
    {
        lock (_lock)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
                var tmp = _filePath + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(progress, _json));
                File.Move(tmp, _filePath, overwrite: true);
            }
            catch (Exception ex)
            {
                AppLogger.Log($"BibleProgressStore save failed: {ex.Message}");
            }
        }
    }
}
