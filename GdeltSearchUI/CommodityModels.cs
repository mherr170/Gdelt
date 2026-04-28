using System.Text.Json.Serialization;

namespace GdeltSearchUI;

// ── API-Ninjas /v1/commodityprice response ─────────────────────────────────────

internal sealed record CommodityPriceResponse
{
    [JsonPropertyName("exchange")]
    public string Exchange { get; init; } = "";

    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("price")]
    public double Price { get; init; }

    [JsonPropertyName("updated")]
    public long Updated { get; init; }
}

// ── Domain model ──────────────────────────────────────────────────────────────

internal sealed record CommodityPrice
{
    public string         Slug        { get; init; } = "";
    public string         DisplayName { get; init; } = "";
    public string         Unit        { get; init; } = "";
    public double         Price       { get; init; }
    public double?        Previous    { get; init; }
    public DateTimeOffset UpdatedAt   { get; init; }
}

internal sealed record CommodityHistoryPoint
{
    [JsonPropertyName("ts")]
    public DateTimeOffset Timestamp { get; init; }

    [JsonPropertyName("p")]
    public Dictionary<string, double> Prices { get; init; } = [];
}

internal sealed record CommodityData
{
    public IReadOnlyList<CommodityPrice>        Prices      { get; init; } = [];
    public IReadOnlyList<CommodityHistoryPoint> History     { get; init; } = [];
    public string?                              ErrorMessage { get; init; }
    public bool                                 IsSuccess => ErrorMessage is null;
}
