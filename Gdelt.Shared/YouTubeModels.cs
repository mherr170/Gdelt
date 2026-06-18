using System.Text.Json.Serialization;

namespace GdeltSearchUI;

internal sealed record YouTubeVideo(string VideoId, string Title, long ViewCount)
{
    public string WatchUrl => $"https://www.youtube.com/watch?v={VideoId}";
}

// ── YouTube Data API v3 response shapes ──────────────────────────────────────

internal sealed class ChannelsListResponse
{
    [JsonPropertyName("items")] public ChannelItem[] Items { get; init; } = [];
}

internal sealed class ChannelItem
{
    [JsonPropertyName("contentDetails")] public ChannelContentDetails ContentDetails { get; init; } = new();
}

internal sealed class ChannelContentDetails
{
    [JsonPropertyName("relatedPlaylists")] public RelatedPlaylists RelatedPlaylists { get; init; } = new();
}

internal sealed class RelatedPlaylists
{
    [JsonPropertyName("uploads")] public string Uploads { get; init; } = "";
}

internal sealed class PlaylistItemsListResponse
{
    [JsonPropertyName("nextPageToken")] public string?              NextPageToken { get; init; }
    [JsonPropertyName("items")]         public PlaylistItemEntry[]  Items         { get; init; } = [];
}

internal sealed class PlaylistItemEntry
{
    [JsonPropertyName("contentDetails")] public PlaylistItemContent ContentDetails { get; init; } = new();
}

internal sealed class PlaylistItemContent
{
    [JsonPropertyName("videoId")] public string VideoId { get; init; } = "";
}

internal sealed class VideosListResponse
{
    [JsonPropertyName("items")] public VideoItem[] Items { get; init; } = [];
}

internal sealed class VideoItem
{
    [JsonPropertyName("id")]         public string         Id         { get; init; } = "";
    [JsonPropertyName("snippet")]    public VideoSnippet   Snippet    { get; init; } = new();
    [JsonPropertyName("statistics")] public VideoStatistics Statistics { get; init; } = new();
}

internal sealed class VideoSnippet
{
    [JsonPropertyName("title")] public string Title { get; init; } = "";
}

internal sealed class VideoStatistics
{
    [JsonPropertyName("viewCount")] public string ViewCount { get; init; } = "0";
}
