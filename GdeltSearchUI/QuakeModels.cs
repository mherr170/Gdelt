using System.Text.Json.Serialization;

namespace GdeltSearchUI;

// ── USGS GeoJSON response ─────────────────────────────────────────────────────

internal sealed record UsgsFeatureCollection
{
    [JsonPropertyName("features")]
    public List<UsgsFeature> Features { get; init; } = [];
}

internal sealed record UsgsFeature
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("properties")]
    public UsgsProperties? Properties { get; init; }

    [JsonPropertyName("geometry")]
    public UsgsGeometry? Geometry { get; init; }
}

internal sealed record UsgsProperties
{
    [JsonPropertyName("mag")]
    public double? Magnitude { get; init; }

    [JsonPropertyName("place")]
    public string Place { get; init; } = "";

    [JsonPropertyName("time")]
    public long Time { get; init; }              // Unix milliseconds UTC

    [JsonPropertyName("tsunami")]
    public int Tsunami { get; init; }

    [JsonPropertyName("type")]
    public string Type { get; init; } = "";

    public DateTime LocalTime =>
        DateTimeOffset.FromUnixTimeMilliseconds(Time).LocalDateTime;

    public DateTime UtcTime =>
        DateTimeOffset.FromUnixTimeMilliseconds(Time).UtcDateTime;
}

internal sealed record UsgsGeometry
{
    [JsonPropertyName("coordinates")]
    public double[] Coordinates { get; init; } = [];

    public double? Depth     => Coordinates.Length >= 3 ? Coordinates[2] : null;
    public double? Longitude => Coordinates.Length >= 2 ? Coordinates[0] : null;
    public double? Latitude  => Coordinates.Length >= 2 ? Coordinates[1] : null;
}

// ── Domain model ──────────────────────────────────────────────────────────────

internal sealed record QuakeEvent
{
    public string   Id             { get; init; } = "";
    public double   Magnitude      { get; init; }
    public string   Place          { get; init; } = "";
    public DateTime Time           { get; init; }
    public DateTime UtcTime        { get; init; }
    public double?  DepthKm        { get; init; }
    public bool     TsunamiWarning { get; init; }
    public string   EventType      { get; init; } = "";
    public double?  Latitude       { get; init; }
    public double?  Longitude      { get; init; }
}
