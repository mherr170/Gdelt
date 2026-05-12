using System.Net.Http.Json;

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

    private static ApodEntry Map(ApodApiResponse r)
    {
        var isVideo  = r.MediaType?.Equals("video", StringComparison.OrdinalIgnoreCase) == true;
        var imageUrl = isVideo ? r.ThumbnailUrl : (r.Url ?? r.HdUrl);

        return new ApodEntry
        {
            Date        = r.Date        ?? "",
            Title       = r.Title       ?? "",
            Explanation = r.Explanation ?? "",
            ImageUrl    = imageUrl,
            Copyright   = string.IsNullOrWhiteSpace(r.Copyright) ? null : r.Copyright.Trim(),
            IsVideo     = isVideo,
        };
    }

    public void Dispose() => _http.Dispose();
}
