namespace GdeltSearchUI;

internal enum WeatherAutoPostOutcome { Posted, NoNewAlerts, MissingCredentials, Failed }

internal sealed record WeatherAutoPostResult(
    WeatherAutoPostOutcome Outcome,
    int                    PostedCount  = 0,
    string?                ErrorMessage = null);

internal static class WeatherAutoPost
{
    private const string W          = "weather";
    private const int    MaxPerRun  = 3;

    public static async Task<WeatherAutoPostResult> PostIfNeededAsync(CancellationToken ct = default)
    {
        PostLogger.Info(W, "Checking NWS for active high-impact weather alerts…");

        IReadOnlyList<WeatherAlert> alerts;
        try
        {
            using var client = new WeatherApiClient();
            alerts = await client.GetActiveAlertsAsync(ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            PostLogger.Error(W, $"Fetch failed: {ex.Message}");
            return new(WeatherAutoPostOutcome.Failed, ErrorMessage: ex.Message);
        }

        var candidates = alerts
            .Where(a => !WeatherPostTracker.HasBeenPosted(a.Id))
            .OrderByDescending(AlertPriority)
            .Take(MaxPerRun)
            .ToList();

        PostLogger.Info(W, $"{alerts.Count} active target alert(s) — {candidates.Count} new");

        if (candidates.Count == 0)
            return new(WeatherAutoPostOutcome.NoNewAlerts);

        var creds = CredentialManager.LoadWeatherBluesky();
        if (creds is null)
        {
            PostLogger.Warn(W, $"{candidates.Count} new alert(s) but no Bluesky credentials configured");
            return new(WeatherAutoPostOutcome.MissingCredentials);
        }

        int posted = 0;
        using var poster = new BlueskyPoster();

        foreach (var alert in candidates)
        {
            ct.ThrowIfCancellationRequested();

            PostLogger.Info(W, $"Posting: {alert.Event} — {TrimTo(alert.AreaDesc, 60)}");

            var (headline, tags) = await LmStudioPostGenerator.GenerateWeatherAlertPostAsync(alert);
            var text = BuildPostText(alert, headline, tags);

            var (ok, error) = await poster.PostTextAsync(
                creds.Value.Handle, creds.Value.Password, text, ct);

            if (ok)
            {
                WeatherPostTracker.MarkPosted(alert.Id);
                posted++;
                PostLogger.Success(W, $"Posted: {alert.Event} | {TrimTo(alert.AreaDesc, 60)}");
            }
            else
            {
                PostLogger.Error(W, $"Post failed for {alert.Event}: {error}");
            }

            if (posted < candidates.Count)
                await Task.Delay(2000, ct);
        }

        return posted == 0
            ? new(WeatherAutoPostOutcome.Failed, ErrorMessage: "All posts failed")
            : new(WeatherAutoPostOutcome.Posted, posted);
    }

    internal static string BuildPostText(WeatherAlert a, string headline, string[] tags)
    {
        var emoji       = AlertEmoji(a.Event);
        var expiresStr  = a.Expires.HasValue ? a.Expires.Value.ToString("h:mm tt") : "—";
        var area        = TrimTo(a.AreaDesc, 100);
        var sender      = a.SenderName.Replace("National Weather Service ", "NWS ");

        var allTags     = tags.Prepend("WeatherAlert").Distinct().ToArray();
        var hashtagLine = BlueskyPostHelper.HashtagLine(allTags);

        return
            $"{emoji} {a.Event}\n\n" +
            $"{headline}\n\n" +
            $"📍 {area}\n" +
            $"⏰ Expires: {expiresStr}\n" +
            $"Src: {sender}" +
            hashtagLine;
    }

    private static int AlertPriority(WeatherAlert a) => a.Event switch
    {
        var e when e.Contains("Emergency",  StringComparison.OrdinalIgnoreCase) => 100,
        var e when e.Contains("Tornado",    StringComparison.OrdinalIgnoreCase) => 90,
        var e when e.Contains("Tsunami",    StringComparison.OrdinalIgnoreCase) => 85,
        var e when e.Contains("Hurricane",  StringComparison.OrdinalIgnoreCase) => 80,
        var e when e.Contains("Typhoon",    StringComparison.OrdinalIgnoreCase) => 80,
        var e when e.Contains("Evacuation", StringComparison.OrdinalIgnoreCase) => 75,
        var e when e.Contains("Blizzard",   StringComparison.OrdinalIgnoreCase) => 60,
        var e when e.Contains("Wind",       StringComparison.OrdinalIgnoreCase) => 50,
        _                                                                        => 40,
    };

    private static string AlertEmoji(string evt) => evt switch
    {
        var e when e.Contains("Tornado",    StringComparison.OrdinalIgnoreCase) => "🌪️",
        var e when e.Contains("Hurricane",  StringComparison.OrdinalIgnoreCase) => "🌀",
        var e when e.Contains("Typhoon",    StringComparison.OrdinalIgnoreCase) => "🌀",
        var e when e.Contains("Tsunami",    StringComparison.OrdinalIgnoreCase) => "🌊",
        var e when e.Contains("Blizzard",   StringComparison.OrdinalIgnoreCase) => "🌨️",
        var e when e.Contains("Flood",      StringComparison.OrdinalIgnoreCase) => "🌊",
        var e when e.Contains("Wind",       StringComparison.OrdinalIgnoreCase) => "💨",
        _                                                                        => "⚠️",
    };

    private static string TrimTo(string s, int max) =>
        s.Length <= max ? s : s[..(max - 1)] + "…";
}
