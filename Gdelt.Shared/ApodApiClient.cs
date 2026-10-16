using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace GdeltSearchUI;

// Thrown when the NASA API kept failing with a transient error (5xx, 429, timeout,
// network) through every retry AND the science.nasa.gov/apod fallback also failed.
// Callers treat it as "NASA is down, try again later" rather than a hard failure.
internal sealed class ApodUnavailableException(string message) : Exception(message);

internal sealed class ApodApiClient : IDisposable
{
    private const string BaseUrl = "https://api.nasa.gov/planetary/apod";

    // Backoff between API attempts; one more attempt than there are delays.
    internal static readonly TimeSpan[] DefaultRetryDelays =
        [TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30)];

    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly IReadOnlyList<TimeSpan> _retryDelays;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    public ApodApiClient(string apiKey)
        : this(apiKey, new HttpClientHandler(), DefaultRetryDelays, Task.Delay) { }

    internal ApodApiClient(
        string apiKey,
        HttpMessageHandler handler,
        IReadOnlyList<TimeSpan> retryDelays,
        Func<TimeSpan, CancellationToken, Task> delay)
    {
        _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
        _http.DefaultRequestHeaders.Add("User-Agent", "GdeltAutoPost/1.0");
        _apiKey      = apiKey;
        _retryDelays = retryDelays;
        _delay       = delay;
    }

    public async Task<ApodEntry?> GetTodayAsync(CancellationToken ct = default)
    {
        var url = $"{BaseUrl}?api_key={Uri.EscapeDataString(_apiKey)}&thumbs=true";
        ApodApiResponse? response = null;
        string? apiError = null;
        var transient = false;

        for (var attempt = 0; ; attempt++)
        {
            try
            {
                response = await _http.GetFromJsonAsync<ApodApiResponse>(url, ct);
                apiError = null;
                break;
            }
            catch (Exception ex) when (IsTransient(ex, ct))
            {
                apiError  = Describe(ex);
                transient = true;
                if (attempt >= _retryDelays.Count) break;

                var wait = _retryDelays[attempt];
                PostLogger.Info("apod", $"APOD API {apiError} — retry {attempt + 1}/{_retryDelays.Count} in {wait.TotalSeconds:F0}s");
                await _delay(wait, ct);
            }
            catch (Exception ex) when (ex is HttpRequestException or JsonException)
            {
                // Non-transient (bad key, malformed body): retrying won't help, go straight to the page.
                apiError  = Describe(ex);
                transient = false;
                break;
            }
        }

        if (apiError is not null)
        {
            PostLogger.Warn("apod", $"APOD API failed ({apiError}) — trying science.nasa.gov/apod page");
            var scraped = await ScrapeApodPageAsync(null, ct);
            if (scraped is null && transient)
                throw new ApodUnavailableException(
                    $"NASA APOD API unavailable ({apiError} after {_retryDelays.Count + 1} attempts) and APOD page fallback failed");
            return scraped;
        }

        if (response is null) return null;

        // NASA's API has been returning the site logo as the "image" (with a stale caption).
        // Never post that — fall back to the real APOD page, or give up so nothing is posted.
        if (IsPlaceholder(response))
            return await ScrapeApodPageAsync(response, ct);

        return Map(response);
    }

    // 5xx, 408, 429, connection failures and HttpClient timeouts are worth retrying;
    // a cancellation requested by the caller is not.
    private static bool IsTransient(Exception ex, CancellationToken ct) => ex switch
    {
        TaskCanceledException                 => !ct.IsCancellationRequested,
        HttpRequestException { StatusCode: null } => true,
        HttpRequestException { StatusCode: { } code } =>
            (int)code >= 500 || code is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests,
        _ => false,
    };

    private static string Describe(Exception ex) => ex switch
    {
        TaskCanceledException                     => "timed out",
        HttpRequestException { StatusCode: { } c } => $"HTTP {(int)c}",
        _                                         => ex.Message,
    };

    private static bool IsPlaceholder(ApodApiResponse r) =>
        (r.Url ?? "").Contains("nasa-logo", StringComparison.OrdinalIgnoreCase) ||
        (r.HdUrl ?? "").Contains("nasa-logo", StringComparison.OrdinalIgnoreCase);

    private static readonly Regex _heroImg   = new(@"class=""media-detail-hero__media[^""]*"">.*?<img[^>]+src=""([^""]+)""", RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex _heroTitle = new(@"</figure>.*?<h2[^>]*>(.*?)</h2>", RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex _credit    = new(@"Credit\s*&amp;\s*Copyright</th>\s*<td[^>]*>(.*?)</td>", RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex _tags      = new(@"<[^>]+>", RegexOptions.Compiled);

    private static readonly Regex _desc = new(@"media-detail-hero__description"">\s*<strong>Explanation:</strong>(.*?)(?:<br|</p>)", RegexOptions.Singleline | RegexOptions.Compiled);

    private static string ExplanationFrom(string html)
    {
        var m = _desc.Match(html);
        return m.Success ? System.Net.WebUtility.HtmlDecode(_tags.Replace(m.Groups[1].Value, "")).Trim() : "";
    }

    private async Task<ApodEntry?> ScrapeApodPageAsync(ApodApiResponse? api, CancellationToken ct)
    {
        var why = api is null ? "APOD API unavailable" : "API returned NASA logo placeholder";
        string html;
        try { html = await _http.GetStringAsync("https://science.nasa.gov/apod/", ct); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            PostLogger.Warn("apod", $"{why} and APOD page fetch failed: {ex.Message}");
            return null;
        }

        var img   = _heroImg.Match(html);
        var title = _heroTitle.Match(html);
        if (!img.Success || !title.Success)
        {
            PostLogger.Warn("apod", $"{why} and APOD page could not be parsed");
            return null;
        }

        static string Clean(string h) => System.Net.WebUtility.HtmlDecode(_tags.Replace(h, "")).Trim();

        var credit = _credit.Match(html);
        var copyright = credit.Success ? Clean(credit.Groups[1].Value) : null;

        PostLogger.Info("apod", $"{why} — using science.nasa.gov/apod page instead");
        return new ApodEntry
        {
            Date        = api?.Date ?? DateTime.Now.ToString("yyyy-MM-dd"),
            Title       = Clean(title.Groups[1].Value),
            Explanation = api?.Explanation ?? ExplanationFrom(html),
            ImageUrl    = System.Net.WebUtility.HtmlDecode(img.Groups[1].Value),
            Copyright   = string.IsNullOrWhiteSpace(copyright) ? null : copyright,
            IsVideo     = false,
        };
    }

    private static readonly Regex _ytEmbed = new(@"youtube\.com/embed/([^?&/]+)", RegexOptions.Compiled);

    private static ApodEntry Map(ApodApiResponse r)
    {
        var isVideo  = r.MediaType?.Equals("video", StringComparison.OrdinalIgnoreCase) == true;
        var imageUrl = isVideo ? r.ThumbnailUrl : (r.Url ?? r.HdUrl);
        var videoUrl = isVideo ? ToWatchUrl(r.Url) : null;

        return new ApodEntry
        {
            Date        = r.Date        ?? "",
            Title       = r.Title       ?? "",
            Explanation = r.Explanation ?? "",
            ImageUrl    = imageUrl,
            VideoUrl    = videoUrl,
            Copyright   = string.IsNullOrWhiteSpace(r.Copyright) ? null : r.Copyright.Trim(),
            IsVideo     = isVideo,
        };
    }

    // Converts YouTube embed URLs to watch URLs; passes other URLs through unchanged.
    private static string? ToWatchUrl(string? url)
    {
        if (url is null) return null;
        var m = _ytEmbed.Match(url);
        return m.Success ? $"https://www.youtube.com/watch?v={m.Groups[1].Value}" : url;
    }

    public void Dispose() => _http.Dispose();
}
