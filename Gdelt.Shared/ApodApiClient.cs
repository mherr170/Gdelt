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
        var response = await _http.GetFromJsonAsync<ApodApiResponse>(url, ct);
        return response is null ? null : Map(response);
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
