using System.Text.Json.Serialization;

namespace GdeltSearchUI;

// ── OilPriceAPI.com response DTOs ──────────────────────────────────────────

internal sealed record OilPriceDataPoint
{
    [JsonPropertyName("price")]
    public double Price { get; init; }

    [JsonPropertyName("code")]
    public string Code { get; init; } = "";

    [JsonPropertyName("unit")]
    public string Unit { get; init; } = "";

    [JsonPropertyName("created_at")]
    public DateTimeOffset CreatedAt { get; init; }

    [JsonPropertyName("currency")]
    public string Currency { get; init; } = "";

    [JsonPropertyName("formatted")]
    public string Formatted { get; init; } = "";
}

// ── Domain record ──────────────────────────────────────────────────────────

internal sealed record OilPriceEntry(
    string         Code,
    string         DisplayName,
    string         Unit,
    double         Price,
    DateTimeOffset UpdatedAt);
