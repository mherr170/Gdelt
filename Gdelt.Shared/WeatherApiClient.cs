using System.Net.Http.Json;

namespace GdeltSearchUI;

internal sealed class WeatherApiClient : IDisposable
{
    private const string Base = "https://api.weather.gov";

    // NWS requires a User-Agent identifying the app and a contact email.
    private readonly HttpClient _http;

    public WeatherApiClient()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        _http.DefaultRequestHeaders.Add("User-Agent", "(GdeltAutoPost, mherr170@gmail.com)");
        _http.DefaultRequestHeaders.Add("Accept", "application/geo+json");
    }

    // Events that warrant a post — high-impact, not routine
    public static readonly HashSet<string> TargetEvents = new(StringComparer.OrdinalIgnoreCase)
    {
        "Tornado Warning",
        "Tornado Emergency",
        "Hurricane Warning",
        "Hurricane Watch",
        "Typhoon Warning",
        "Typhoon Watch",
        "Blizzard Warning",
        "Flash Flood Emergency",
        "Extreme Wind Warning",
        "Tsunami Warning",
        "Tsunami Advisory",
        "Tsunami Watch",
        "Evacuation - Immediate",
    };

    public async Task<IReadOnlyList<WeatherAlert>> GetActiveAlertsAsync(CancellationToken ct = default)
    {
        var url = $"{Base}/alerts/active?severity=Extreme,Severe&urgency=Immediate,Expected";

        using var resp = await _http.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(
                $"NWS returned {(int)resp.StatusCode} {resp.ReasonPhrase}: {(body.Length > 300 ? body[..300] : body)}",
                null, resp.StatusCode);
        }

        var response = await resp.Content.ReadFromJsonAsync<NwsAlertCollection>(ct);

        if (response?.Features is not { } features) return [];

        return features
            .Where(f => f.Properties is not null && f.Id is not null)
            .Where(f => TargetEvents.Contains(f.Properties!.Event ?? ""))
            .Select(Map)
            .ToList();
    }

    private static WeatherAlert Map(NwsFeature f)
    {
        var p = f.Properties!;
        return new WeatherAlert
        {
            Id          = f.Id!,
            Event       = p.Event       ?? "",
            Headline    = p.Headline    ?? "",
            Description = p.Description ?? "",
            AreaDesc    = p.AreaDesc    ?? "",
            SenderName  = p.SenderName  ?? "",
            Severity    = p.Severity    ?? "",
            Urgency     = p.Urgency     ?? "",
            Onset       = TryParseDate(p.Onset),
            Expires     = TryParseDate(p.Expires),
            Instruction = p.Instruction,
            TrackingKey = VtecKey(p.Parameters?.Vtec) ?? f.Id!,
        };
    }

    private static DateTime? TryParseDate(string? s) =>
        DateTimeOffset.TryParse(s, out var dt) ? dt.LocalDateTime : null;

    // VTEC format: "/k.aaa.CCCC.pp.s.####.start-end/" where k=class, aaa=action
    // (NEW/CON/EXT/...), CCCC=office, pp=phenomena, s=significance, ####=event
    // tracking number. The action code and times change on every update to a
    // warning, but office+phenomena+significance+ETN identifies the same event
    // throughout its lifetime — that's what we key on to avoid reposting an
    // update to a warning we've already posted.
    private static string? VtecKey(string[]? vtec)
    {
        var line = vtec?.FirstOrDefault();
        if (string.IsNullOrEmpty(line)) return null;

        var parts = line.Trim('/').Split('.');
        if (parts.Length < 6) return null;

        return $"{parts[2]}.{parts[3]}.{parts[4]}.{parts[5]}";
    }

    public void Dispose() => _http.Dispose();
}
