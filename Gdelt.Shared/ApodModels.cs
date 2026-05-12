using System.Text.Json.Serialization;

namespace GdeltSearchUI;

internal sealed class ApodApiResponse
{
    [JsonPropertyName("date")]          public string? Date         { get; init; }
    [JsonPropertyName("title")]         public string? Title        { get; init; }
    [JsonPropertyName("explanation")]   public string? Explanation  { get; init; }
    [JsonPropertyName("url")]           public string? Url          { get; init; }
    [JsonPropertyName("hdurl")]         public string? HdUrl        { get; init; }
    [JsonPropertyName("media_type")]    public string? MediaType    { get; init; }
    [JsonPropertyName("copyright")]     public string? Copyright    { get; init; }
    [JsonPropertyName("thumbnail_url")] public string? ThumbnailUrl { get; init; }
}

internal sealed record ApodEntry
{
    public string  Date        { get; init; } = "";
    public string  Title       { get; init; } = "";
    public string  Explanation { get; init; } = "";
    public string? ImageUrl    { get; init; }
    public string? Copyright   { get; init; }
    public bool    IsVideo     { get; init; }
}
