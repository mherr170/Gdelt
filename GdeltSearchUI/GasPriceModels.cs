using System.Text.Json.Serialization;

namespace GdeltSearchUI;

// ── EIA API v2 response shape ─────────────────────────────────────────────────

internal sealed record EiaResponse
{
    [JsonPropertyName("response")]
    public EiaResponseBody? Response { get; init; }
}

internal sealed record EiaResponseBody
{
    [JsonPropertyName("data")]
    public List<EiaDataPoint> Data { get; init; } = [];
}

internal sealed record EiaDataPoint
{
    [JsonPropertyName("period")]
    public string Period { get; init; } = "";

    [JsonPropertyName("product")]
    public string Product { get; init; } = "";

    [JsonPropertyName("value")]
    public string? Value { get; init; }
}

// ── Domain result ─────────────────────────────────────────────────────────────

internal sealed record NationalGasPrices
{
    public double? Regular  { get; init; }
    public double? MidGrade { get; init; }
    public double? Premium  { get; init; }
    public double? Diesel   { get; init; }
    public string  Period   { get; init; } = "";

    public string? ErrorMessage { get; init; }
    public bool IsSuccess => ErrorMessage is null;
}
