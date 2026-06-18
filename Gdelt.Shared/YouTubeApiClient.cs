using System.Net.Http.Json;

namespace GdeltSearchUI;

internal sealed class YouTubeApiClient : IDisposable
{
    private const string Base = "https://www.googleapis.com/youtube/v3";
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private readonly string _apiKey;

    public YouTubeApiClient(string apiKey) => _apiKey = apiKey;

    // Returns the top N videos from a channel (by forHandle) sorted by view count descending.
    // Fetches the uploads playlist (up to 200 videos) then ranks by views.
    public async Task<IReadOnlyList<YouTubeVideo>> GetTopVideosByViewCountAsync(
        string channelHandle, int count, CancellationToken ct = default)
    {
        var uploadsId = await GetUploadsPlaylistIdAsync(channelHandle, ct);
        if (string.IsNullOrEmpty(uploadsId))
            throw new InvalidOperationException($"Could not resolve uploads playlist for @{channelHandle}");

        var videoIds = await GetAllVideoIdsAsync(uploadsId, maxVideos: 200, ct);
        if (videoIds.Count == 0) return [];

        var videos = await GetVideoDetailsAsync(videoIds, ct);
        return [.. videos.OrderByDescending(v => v.ViewCount).Take(count)];
    }

    private async Task<string> GetUploadsPlaylistIdAsync(string channelHandle, CancellationToken ct)
    {
        var url = $"{Base}/channels?part=contentDetails&forHandle={Uri.EscapeDataString(channelHandle)}&key={_apiKey}";
        var resp = await _http.GetFromJsonAsync<ChannelsListResponse>(url, ct);
        return resp?.Items.FirstOrDefault()?.ContentDetails.RelatedPlaylists.Uploads ?? "";
    }

    private async Task<List<string>> GetAllVideoIdsAsync(string playlistId, int maxVideos, CancellationToken ct)
    {
        var ids = new List<string>();
        string? pageToken = null;

        while (ids.Count < maxVideos)
        {
            var pagePart = pageToken is not null ? $"&pageToken={pageToken}" : "";
            var url = $"{Base}/playlistItems?part=contentDetails&playlistId={playlistId}&maxResults=50{pagePart}&key={_apiKey}";
            var resp = await _http.GetFromJsonAsync<PlaylistItemsListResponse>(url, ct);
            if (resp is null) break;

            ids.AddRange(resp.Items.Select(i => i.ContentDetails.VideoId).Where(id => id.Length > 0));
            pageToken = resp.NextPageToken;
            if (pageToken is null) break;
        }

        return ids;
    }

    private async Task<List<YouTubeVideo>> GetVideoDetailsAsync(List<string> videoIds, CancellationToken ct)
    {
        var result = new List<YouTubeVideo>();

        // videos.list accepts up to 50 IDs per request
        for (int i = 0; i < videoIds.Count; i += 50)
        {
            var batch = videoIds.Skip(i).Take(50);
            var ids   = string.Join(",", batch.Select(Uri.EscapeDataString));
            var url   = $"{Base}/videos?part=snippet,statistics&id={ids}&key={_apiKey}";
            var resp  = await _http.GetFromJsonAsync<VideosListResponse>(url, ct);
            if (resp is null) continue;

            foreach (var item in resp.Items)
            {
                if (long.TryParse(item.Statistics.ViewCount, out var views))
                    result.Add(new YouTubeVideo(item.Id, item.Snippet.Title, views));
            }
        }

        return result;
    }

    public void Dispose() => _http.Dispose();
}
