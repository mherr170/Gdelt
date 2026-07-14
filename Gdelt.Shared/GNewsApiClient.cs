using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GdeltSearchUI;

// Fallback source for GunViolenceAutoPost when GDELT is rate-limited. Free tier
// is 100 req/day and articles arrive up to ~12h late, so callers should use a
// wider lookback window than they would for GDELT to actually get results.
public sealed record GNewsSearchResult
{
    public List<GdeltArticle> Articles { get; init; } = [];
    public string?            ErrorMessage   { get; init; }
    public bool                RateLimited    { get; init; } // 429 — per-second limit
    public bool                QuotaExceeded  { get; init; } // 403 — daily quota gone

    public bool IsSuccess => ErrorMessage is null && !RateLimited && !QuotaExceeded;
}

internal sealed class GNewsResponse
{
    [JsonPropertyName("articles")]
    public List<GNewsArticle> Articles { get; init; } = [];
}

internal sealed class GNewsArticle
{
    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; init; } = string.Empty;

    [JsonPropertyName("publishedAt")]
    public string PublishedAt { get; init; } = string.Empty;

    [JsonPropertyName("source")]
    public GNewsSource Source { get; init; } = new();
}

internal sealed class GNewsSource
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; init; } = string.Empty;
}

public sealed class GNewsApiClient : IDisposable
{
    private const string BaseUrl = "https://gnews.io/api/v4/search";

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };

    public string BuildUrl(string apiKey, string query, int hours, int maxRecords)
    {
        var encodedQuery = Uri.EscapeDataString(query);
        var from = DateTime.UtcNow.AddHours(-hours).ToString("yyyy-MM-ddTHH:mm:ssZ");
        return $"{BaseUrl}?q={encodedQuery}&lang=en&country=us&max={maxRecords}" +
               $"&from={from}&sortby=publishedAt&apikey={Uri.EscapeDataString(apiKey)}";
    }

    public async Task<GNewsSearchResult> SearchAsync(
        string apiKey, string query, int hours, CancellationToken ct, int maxRecords = 10)
    {
        var url = BuildUrl(apiKey, query, hours, maxRecords);
        string raw;
        try
        {
            AppLogger.Log($"GET {BaseUrl} (GNews fallback, {hours}h lookback)");
            raw = await _http.GetStringAsync(url, ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
        {
            AppLogger.Log("GNews rate limited (429).");
            return new GNewsSearchResult { RateLimited = true };
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Forbidden)
        {
            AppLogger.Log("GNews daily quota exceeded (403).");
            return new GNewsSearchResult { QuotaExceeded = true };
        }
        catch (HttpRequestException ex)
        {
            AppLogger.Log($"GNews network error: {ex.Message}");
            return new GNewsSearchResult { ErrorMessage = ex.Message };
        }

        GNewsResponse? response;
        try
        {
            response = JsonSerializer.Deserialize<GNewsResponse>(raw);
        }
        catch (JsonException ex)
        {
            AppLogger.Log($"GNews JSON parse failed: {ex.Message}");
            return new GNewsSearchResult { ErrorMessage = $"JSON parse error: {ex.Message}" };
        }

        var articles = ArticleFilter.FilterByDomain(
            (response?.Articles ?? []).Select(ToGdeltArticle));
        AppLogger.Log($"GNews returned {articles.Count} article(s) after filtering.");

        return new GNewsSearchResult { Articles = articles };
    }

    private static GdeltArticle ToGdeltArticle(GNewsArticle a)
    {
        var domain = Uri.TryCreate(a.Source.Url, UriKind.Absolute, out var uri)
            ? uri.Host.StartsWith("www.") ? uri.Host[4..] : uri.Host
            : a.Source.Name;

        var seenDate = DateTime.TryParse(a.PublishedAt, null,
            System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal,
            out var dt)
            ? dt.ToString("yyyyMMddTHHmmssZ")
            : "";

        return new GdeltArticle
        {
            Url           = a.Url,
            Title         = a.Title,
            SeenDate      = seenDate,
            Domain        = domain,
            Language      = "English",
            SourceCountry = "United States", // GNews has no per-article field; relies on country=us request param
            Tone          = 0,
        };
    }

    public void Dispose() => _http.Dispose();
}
