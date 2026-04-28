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
                Latitude       = f.Geometry?.Latitude,
                Longitude      = f.Geometry?.Longitude,
            })
            .ToList();

        return (events, null);
    }

    public async Task<List<QuakeEvent>> GetNearbyAsync(
        double latitude, double longitude, double radiusKm, int hours,
        double minMagnitude, CancellationToken ct = default)
    {
        var start = DateTime.UtcNow.AddHours(-hours).ToString("yyyy-MM-ddTHH:mm:ss");
        var url = $"{BaseUrl}?format=geojson" +
                  $"&latitude={latitude:F4}&longitude={longitude:F4}&maxradiuskm={radiusKm:F0}" +
                  $"&minmagnitude={minMagnitude}&starttime={start}&orderby=time&limit=200";

        try
        {
            var response = await _http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode) return [];

            var json = await response.Content.ReadAsStringAsync(ct);
            var parsed = JsonSerializer.Deserialize<UsgsFeatureCollection>(json);

            return (parsed?.Features ?? [])
                .Where(f => f.Properties != null && f.Geometry?.Latitude.HasValue == true)
                .Select(f => new QuakeEvent
                {
                    Id             = f.Id,
                    Magnitude      = f.Properties!.Magnitude ?? 0,
                    Place          = f.Properties.Place,
                    UtcTime        = f.Properties.UtcTime,
                    DepthKm        = f.Geometry?.Depth,
                    TsunamiWarning = f.Properties.Tsunami > 0,
                    Latitude       = f.Geometry?.Latitude,
                    Longitude      = f.Geometry?.Longitude,
                })
                .ToList();
        }
        catch (OperationCanceledException) { throw; }
        catch { return []; }
    }

    public void Dispose() => _http.Dispose();
}
