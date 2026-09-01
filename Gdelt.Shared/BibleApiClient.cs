using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace GdeltSearchUI;

// Minimal client for bible-api.com — a free, no-auth API serving public-domain
// Bible translations. We request the World English Bible (WEB), which is safe to
// republish automatically (KJV etc. are also available via ?translation=).
internal sealed class BibleApiClient : IDisposable
{
    private const string BaseUrl = "https://bible-api.com";
    private readonly HttpClient _http;

    public BibleApiClient()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("GdeltAutoPost/1.0 (+bluesky faith verse bot)");
    }

    // Looks up a passage reference like "John 3:16" or "Romans 8:38-39".
    // Returns null on any failure — the caller retries on the next tick.
    public async Task<BibleVerse?> GetVerseAsync(string reference, CancellationToken ct)
    {
        try
        {
            var url  = $"{BaseUrl}/{Uri.EscapeDataString(reference)}?translation=web";
            var resp = await _http.GetFromJsonAsync<BibleApiResponse>(url, ct);
            if (resp is null || string.IsNullOrWhiteSpace(resp.Text)) return null;

            var text       = NormalizeWhitespace(resp.Text);
            var displayRef  = string.IsNullOrWhiteSpace(resp.Reference) ? reference : resp.Reference.Trim();
            return new BibleVerse(displayRef, text, "WEB");
        }
        catch (OperationCanceledException) { throw; }
        catch { return null; }
    }

    // Fetches a whole chapter in one call, returning every verse in order.
    // `singleChapterVerses` > 0 is passed for the five one-chapter books, where
    // "Philemon 1" would be read as a verse — those are fetched as an explicit
    // "Philemon 1:1-25" range instead. Returns null on any failure.
    public async Task<IReadOnlyList<ChapterVerse>?> GetChapterAsync(
        string book, int chapter, int singleChapterVerses, CancellationToken ct)
    {
        try
        {
            var query = singleChapterVerses > 0
                ? $"{book} {chapter}:1-{singleChapterVerses}"
                : $"{book} {chapter}";
            var url  = $"{BaseUrl}/{Uri.EscapeDataString(query)}?translation=web";
            var resp = await _http.GetFromJsonAsync<BibleApiChapterResponse>(url, ct);
            if (resp?.Verses is null || resp.Verses.Count == 0) return null;

            var verses = resp.Verses
                .Select(v => new ChapterVerse(v.Verse, NormalizeWhitespace(v.Text ?? "")))
                .Where(v => v.Text.Length > 0)
                .ToList();
            return verses.Count > 0 ? verses : null;
        }
        catch (OperationCanceledException) { throw; }
        catch { return null; }
    }

    // bible-api.com returns verse text with embedded newlines and runs of spaces;
    // collapse it to a single clean line for the post body.
    private static string NormalizeWhitespace(string s) =>
        string.Join(' ', s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).Trim();

    public void Dispose() => _http.Dispose();
}

internal sealed record BibleVerse(string Reference, string Text, string Translation);
internal sealed record ChapterVerse(int Verse, string Text);

internal sealed class BibleApiResponse
{
    [JsonPropertyName("reference")]        public string? Reference { get; set; }
    [JsonPropertyName("text")]             public string? Text { get; set; }
    [JsonPropertyName("translation_name")] public string? TranslationName { get; set; }
}

internal sealed class BibleApiChapterResponse
{
    [JsonPropertyName("reference")] public string? Reference { get; set; }
    [JsonPropertyName("verses")]    public List<BibleApiVerse>? Verses { get; set; }
}

internal sealed class BibleApiVerse
{
    [JsonPropertyName("chapter")] public int Chapter { get; set; }
    [JsonPropertyName("verse")]   public int Verse { get; set; }
    [JsonPropertyName("text")]    public string? Text { get; set; }
}
