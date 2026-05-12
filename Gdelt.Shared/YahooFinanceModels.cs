using System.Text.Json.Serialization;

namespace GdeltSearchUI;

// ── Yahoo Finance /v8/finance/chart response DTOs ─────────────────────────

internal sealed record YahooChartResponse
{
    [JsonPropertyName("chart")]
    public YahooChart? Chart { get; init; }
}

internal sealed record YahooChart
{
    [JsonPropertyName("result")]
    public List<YahooChartResult>? Result { get; init; }
}

internal sealed record YahooChartResult
{
    [JsonPropertyName("meta")]
    public YahooMeta? Meta { get; init; }
}

internal sealed record YahooMeta
{
    [JsonPropertyName("symbol")]
    public string Symbol { get; init; } = "";

    [JsonPropertyName("regularMarketPrice")]
    public double RegularMarketPrice { get; init; }

    [JsonPropertyName("previousClose")]
    public double PreviousClose { get; init; }

    [JsonPropertyName("regularMarketTime")]
    public long RegularMarketTime { get; init; }
}

// ── Domain record ──────────────────────────────────────────────────────────

internal sealed record OilPriceEntry(
    string         Code,
    string         DisplayName,
    string         Unit,
    double         Price,
    double?        Previous,    // previousClose from Yahoo — used for delta arrows
    DateTimeOffset UpdatedAt);
