using System.Net.Http.Json;
using System.Text.RegularExpressions;

namespace GdeltSearchUI;

internal sealed class ApodApiClient : IDisposable
{
    private const string BaseUrl = "https://api.nasa.gov/planetary/apod";

    private readonly HttpClient _http;

    public ApodApiClient(string apiKey)
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _http.DefaultRequestHeaders.Add("User-Agent", "GdeltAutoPost/1.0");
        _apiKey = apiKey;
    }

    private readonly string _apiKey;

    public async Task<ApodEntry?> GetTodayAsync(CancellationToken ct = default)
    {
        var url      = $"{BaseUrl}?api_key={Uri.EscapeDataString(_apiKey)}&thumbs=true";
        ApodApiResponse? response;
        try { response = await _http.GetFromJsonAsync<ApodApiResponse>(url, ct); }
        catch (HttpRequestException ex)
        {
            PostLogger.Warn("apod", $"APOD API failed ({ex.Message}) — trying science.nasa.gov/apod page");
            return await ScrapeApodPageAsync(null, ct);
        }
        if (response is null) return null;

        // NASA's API has been returning the site logo as the "image" (with a stale caption).
        // Never post that — fall back to the real APOD page, or give up so nothing is posted.
        if (IsPlaceholder(response))
            return await ScrapeApodPageAsync(response, ct);

        return Map(response);
    }

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
        string html;
        try { html = await _http.GetStringAsync("https://science.nasa.gov/apod/", ct); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            PostLogger.Warn("apod", $"API returned NASA logo placeholder and APOD page fetch failed: {ex.Message}");
            return null;
        }

        var img   = _heroImg.Match(html);
        var title = _heroTitle.Match(html);
        if (!img.Success || !title.Success)
        {
            PostLogger.Warn("apod", "API returned NASA logo placeholder and APOD page could not be parsed");
            return null;
        }

        static string Clean(string h) => System.Net.WebUtility.HtmlDecode(_tags.Replace(h, "")).Trim();

        var credit = _credit.Match(html);
        var copyright = credit.Success ? Clean(credit.Groups[1].Value) : null;

        PostLogger.Info("apod", "API returned NASA logo placeholder — using science.nasa.gov/apod page instead");
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
