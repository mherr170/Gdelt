using System.Net;
using System.Text.Json;

namespace GdeltSearchUI;

public sealed record GdeltSearchResult
{
    public List<GdeltArticle> Articles { get; init; } = [];
    public string? ErrorMessage { get; init; }
    public bool TimedOut { get; init; }
    public bool Cancelled { get; init; }
    public string? RawResponse { get; init; }
    public bool FromCache { get; init; }

    public bool RateLimited { get; init; }

    public bool IsSuccess => ErrorMessage is null && !TimedOut && !Cancelled && !RateLimited;
}

public sealed class GdeltApiClient : IDisposable
{
    private const string BaseUrl = "https://api.gdeltproject.org/api/v2/doc/doc";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(60) };
    private readonly Dictionary<string, (GdeltSearchResult Result, DateTime CachedAt)> _cache = [];

    private string CacheKey(string query, int hours, string mode) =>
        $"{query.ToLowerInvariant()}|{hours}|{mode}";

    public string BuildUrl(string query, int hours, string mode, int maxRecords = 75)
    {
        var encodedQuery = Uri.EscapeDataString(query + " sourcelang:english");
        var now = DateTime.UtcNow;
        var start = now.AddHours(-hours).ToString("yyyyMMddHHmmss");
        var end = now.ToString("yyyyMMddHHmmss");
        return $"{BaseUrl}?query={encodedQuery}&mode={mode}&format=json&maxrecords={maxRecords}&startdatetime={start}&enddatetime={end}";
    }

    public async Task<GdeltSearchResult> SearchAsync(string query, int hours, string mode, CancellationToken ct, int maxRecords = 75, bool retryOn429 = true)
    {
        var key = CacheKey(query, hours, mode);
        if (maxRecords == 75 && _cache.TryGetValue(key, out var cached) && DateTime.UtcNow - cached.CachedAt < CacheTtl)
        {
            var age = (int)(DateTime.UtcNow - cached.CachedAt).TotalSeconds;
            AppLogger.Log($"Cache hit for '{query}' ({hours}h, {mode}) — {age}s old.");
            return cached.Result with { FromCache = true };
        }

        var (raw, rateLimited) = await FetchWithRetryAsync(BuildUrl(query, hours, mode, maxRecords), ct, retryOn429);
        if (rateLimited) return new GdeltSearchResult { RateLimited = true };
        if (raw is null)  return new GdeltSearchResult { TimedOut = true };

        AppLogger.Log($"Response ({raw.Length} chars): {raw[..Math.Min(500, raw.Length)]}");

        GdeltResponse? response;
        try
        {
            response = JsonSerializer.Deserialize<GdeltResponse>(raw);
        }
        catch (JsonException ex)
        {
            AppLogger.Log($"JSON parse failed: {ex.Message}");
            return new GdeltSearchResult
            {
                ErrorMessage = $"JSON parse error: {ex.Message}\n\nRaw response (first 1000 chars):\n{raw[..Math.Min(1000, raw.Length)]}",
                RawResponse = raw,
            };
        }

        var articles = ArticleFilter.FilterByDomain(response?.Articles ?? []);
        AppLogger.Log(articles.Count == 0
            ? "Deserialized OK but articles list is empty after filtering."
            : $"{articles.Count} articles after filtering.");

        var result = new GdeltSearchResult { Articles = articles };
        if (maxRecords == 75) _cache[key] = (result, DateTime.UtcNow);
        return result;
    }

    // Returns (content, rateLimited). content is null on timeout after all attempts.
    // Throws on cancellation or network errors. When retryOn429=false, returns (null, true)
    // immediately on 429 so the caller can manage its own retry strategy.
    private async Task<(string? Content, bool RateLimited)> FetchWithRetryAsync(string url, CancellationToken ct, bool retryOn429 = true)
    {
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            AppLogger.Log($"GET {url} (attempt {attempt})");
            try
            {
                return (await _http.GetStringAsync(url, ct), false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                AppLogger.Log("Search cancelled by user.");
                throw;
            }
            catch (OperationCanceledException)
            {
                AppLogger.Log($"Request timed out (attempt {attempt}).");
                if (attempt == 2) return (null, false);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
            {
                if (!retryOn429)
                {
                    AppLogger.Log("Rate limited (429) — returning to caller for retry handling.");
                    return (null, true);
                }
                AppLogger.Log($"Rate limited (429) on attempt {attempt} — waiting 30s before retry.");
                if (attempt == 2) throw;
                await Task.Delay(TimeSpan.FromSeconds(30), ct);
            }
            catch (HttpRequestException ex)
            {
                AppLogger.Log($"Network error: {ex.Message}");
                throw;
            }
        }
        return (null, false);
    }

    public void Dispose() => _http.Dispose();
}
