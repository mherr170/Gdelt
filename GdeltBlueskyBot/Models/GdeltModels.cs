using System.Text.Json.Serialization;

namespace GdeltBlueskyBot.Models;

public sealed class GdeltResponse
{
    [JsonPropertyName("articles")]
    public List<GdeltArticle> Articles { get; init; } = [];
}

public sealed class GdeltArticle
{
    [JsonPropertyName("url")]
    public string Url { get; init; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("seendate")]
    public string SeenDate { get; init; } = string.Empty;

    [JsonPropertyName("domain")]
    public string Domain { get; init; } = string.Empty;

    [JsonPropertyName("language")]
    public string Language { get; init; } = string.Empty;

    [JsonPropertyName("sourcecountry")]
    public string SourceCountry { get; init; } = string.Empty;

    /// <summary>
    /// Sentiment score: negative = negative tone, positive = positive.
    /// We sort by absolute value to surface the most intense coverage.
    /// </summary>
    [JsonPropertyName("tone")]
    public double Tone { get; init; }

    [JsonPropertyName("socialimage")]
    public string? SocialImage { get; init; }
}
