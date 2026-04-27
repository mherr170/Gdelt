using System.Text.Json;

namespace GdeltSearchUI;

/// <summary>
/// Queries the USGS Earthquake Hazards Program API.
/// No API key required — completely free and public.
/// Docs: https://earthquake.usgs.gov/fdsnws/event/1/
/// </summary>
internal sealed class QuakeApiClient : IDisposable
{
    private const string BaseUrl = "https://earthquake.usgs.gov/fdsnws/event/1/query";

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(20) };

    public async Task<(List<QuakeEvent> Events, string? Error)> GetRecentAsync(
        double minMagnitude, int hours, CancellationToken ct = default)
    {
        var start = DateTime.UtcNow.AddHours(-hours).ToString("yyyy-MM-ddTHH:mm:ss");
        var url = $"{BaseUrl}?format=geojson&minmagnitude={minMagnitude}&orderby=time&limit=100&starttime={start}";

        HttpResponseMessage response;
        try
        {
            response = await _http.GetAsync(url, ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return ([], $"Request failed: {ex.Message}"); }

        if (!response.IsSuccessStatusCode)
            return ([], $"USGS API error {(int)response.StatusCode}: {response.ReasonPhrase}");

        var json = await response.Content.ReadAsStringAsync(ct);

        UsgsFeatureCollection? parsed;
        try { parsed = JsonSerializer.Deserialize<UsgsFeatureCollection>(json); }
        catch (JsonException ex) { return ([], $"JSON parse error: {ex.Message}"); }

        var events = (parsed?.Features ?? [])
            .Where(f => f.Properties != null)
            .Select(f => new QuakeEvent
            {
                Id             = f.Id,
                Magnitude      = f.Properties!.Magnitude ?? 0,
                Place          = f.Properties.Place,
                Time           = f.Properties.LocalTime,
                UtcTime        = f.Properties.UtcTime,
                DepthKm        = f.Geometry?.Depth,
                TsunamiWarning = f.Properties.Tsunami > 0,
                EventType      = f.Properties.Type,
            })
            .ToList();

        return (events, null);
    }

    public void Dispose() => _http.Dispose();
}
