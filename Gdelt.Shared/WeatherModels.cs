using System.Text.Json.Serialization;

namespace GdeltSearchUI;

// ── NWS GeoJSON alert response ────────────────────────────────────────────────

internal sealed class NwsAlertCollection
{
    [JsonPropertyName("features")] public List<NwsFeature> Features { get; init; } = [];
}

internal sealed class NwsFeature
{
    [JsonPropertyName("id")]         public string?          Id         { get; init; }
    [JsonPropertyName("properties")] public NwsAlertProps?   Properties { get; init; }
}

internal sealed class NwsAlertProps
{
    [JsonPropertyName("event")]      public string? Event      { get; init; }
    [JsonPropertyName("headline")]   public string? Headline   { get; init; }
    [JsonPropertyName("description")]public string? Description{ get; init; }
    [JsonPropertyName("areaDesc")]   public string? AreaDesc   { get; init; }
    [JsonPropertyName("senderName")] public string? SenderName { get; init; }
    [JsonPropertyName("severity")]   public string? Severity   { get; init; }
    [JsonPropertyName("urgency")]    public string? Urgency    { get; init; }
    [JsonPropertyName("onset")]      public string? Onset      { get; init; }
    [JsonPropertyName("expires")]    public string? Expires    { get; init; }
    [JsonPropertyName("instruction")]public string? Instruction{ get; init; }
}

// ── Domain record ─────────────────────────────────────────────────────────────

internal sealed record WeatherAlert
{
    public string    Id          { get; init; } = "";
    public string    Event       { get; init; } = "";
    public string    Headline    { get; init; } = "";
    public string    Description { get; init; } = "";
    public string    AreaDesc    { get; init; } = "";
    public string    SenderName  { get; init; } = "";
    public string    Severity    { get; init; } = "";
    public string    Urgency     { get; init; } = "";
    public DateTime? Onset       { get; init; }
    public DateTime? Expires     { get; init; }
}
