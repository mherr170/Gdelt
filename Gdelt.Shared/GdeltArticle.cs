using System.Text.Json.Serialization;

namespace GdeltSearchUI;

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

    [JsonPropertyName("tone")]
    public double Tone { get; init; }

    // Parsed on demand for display
    public DateTime? ParsedDate =>
        DateTime.TryParseExact(SeenDate, "yyyyMMddTHHmmssZ",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal, out var dt)
        ? dt.ToLocalTime() : null;
}
