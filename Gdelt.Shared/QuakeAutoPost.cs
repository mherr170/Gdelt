namespace GdeltSearchUI;

internal enum QuakeAutoPostOutcome { Posted, NoNewQuakes, MissingCredentials, Failed }

internal sealed record QuakeAutoPostResult(
    QuakeAutoPostOutcome Outcome,
    int                  PostedCount  = 0,
    string?              ErrorMessage = null);

internal static class QuakeAutoPost
{
    private const string W          = "quake";
    private const double MinMag     = 5.0;
    private const int    LookbackH  = 24;
    private const int    MaxPerRun  = 3;

    public static async Task<QuakeAutoPostResult> PostIfNeededAsync(CancellationToken ct = default)
    {
        PostLogger.Info(W, $"Checking USGS for M{MinMag}+ quakes in past {LookbackH}h…");

        List<QuakeEvent> events;
        using (var client = new QuakeApiClient())
        {
            var (e, err) = await client.GetRecentAsync(MinMag, LookbackH, ct);
            if (err is not null)
            {
                PostLogger.Error(W, $"Fetch failed: {err}");
                return new(QuakeAutoPostOutcome.Failed, ErrorMessage: err);
            }
            events = e;
        }

        var unseen = events
            .Where(q => q.Id.Length > 0 && !QuakePostTracker.HasBeenPosted(q.Id))
            .OrderBy(q => q.UtcTime)
            .Take(MaxPerRun)
            .ToList();

        if (unseen.Count == 0)
        {
            PostLogger.Info(W, $"No new M{MinMag}+ quakes — {events.Count} checked");
            return new(QuakeAutoPostOutcome.NoNewQuakes);
        }

        var creds = CredentialManager.LoadQuakeBluesky();
        if (creds is null)
        {
            PostLogger.Warn(W, $"{unseen.Count} unposted quake(s) found but no Bluesky credentials configured");
            return new(QuakeAutoPostOutcome.MissingCredentials);
        }

        int posted = 0;
        using var poster = new BlueskyPoster();

        for (int i = 0; i < unseen.Count; i++)
        {
            var quake = unseen[i];
            ct.ThrowIfCancellationRequested();

            PostLogger.Info(W, $"Posting M{quake.Magnitude:F1} — {quake.Place} ({quake.Id})…");

            string headline; string body; string[] tags;
            try
            {
                (headline, body, tags) = await LmStudioPostGenerator.GenerateQuakePostAsync(quake);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                PostLogger.Error(W, $"Caption failed for {quake.Id}: {ex.Message}");
                continue;
            }

            var text = BuildPostText(quake, headline, body, tags);

            (bool Ok, string? Error) result;
            if (quake.Latitude.HasValue && quake.Longitude.HasValue)
            {
                byte[] png = [];
                string alt = "";
                try
                {
                    List<QuakeEvent> nearby;
                    using (var client = new QuakeApiClient())
                    {
                        nearby = await client.GetNearbyAsync(
                            quake.Latitude.Value, quake.Longitude.Value,
                            radiusKm: 500, hours: 24, minMagnitude: 3.0, ct);
                    }
                    png = await QuakeMap.RenderPngAsync(quake, nearby, ct: ct);
                    if (png.Length > 0)
                        alt = BuildMapAltText(quake, nearby);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    PostLogger.Warn(W, $"Map fetch/render failed for {quake.Id} ({ex.Message}) — posting text only");
                }

                result = png.Length > 0
                    ? await poster.PostTextWithImageAsync(creds.Value.Handle, creds.Value.Password, text, png, alt, ct)
                    : await poster.PostTextAsync(creds.Value.Handle, creds.Value.Password, text, ct);
            }
            else
            {
                result = await poster.PostTextAsync(
                    creds.Value.Handle, creds.Value.Password, text, ct);
            }

            if (result.Ok)
            {
                QuakePostTracker.MarkPosted(quake.Id);
                posted++;
                PostLogger.Success(W,
                    $"Posted M{quake.Magnitude:F1} — {quake.Place} | " +
                    $"Depth: {(quake.DepthKm.HasValue ? $"{quake.DepthKm.Value:F1}km" : "?")} | " +
                    $"Tsunami: {(quake.TsunamiWarning ? "YES" : "no")} | " +
                    $"Headline: \"{headline}\"");
            }
            else
            {
                PostLogger.Error(W, $"Post failed for {quake.Id}: {result.Error}");
            }

            if (i < unseen.Count - 1)
                await Task.Delay(2000, ct);
        }

        return new(QuakeAutoPostOutcome.Posted, posted);
    }

    internal static string BuildPostText(QuakeEvent q, string headline, string body, string[] tags)
    {
        const int Limit = 300;

        var tsunami   = q.TsunamiWarning ? "\n🌊 Tsunami warning issued" : "";
        var depth     = q.DepthKm.HasValue ? $"{q.DepthKm.Value:F1} km" : "unknown";
        var dataBlock = $"\n\n🌍 M {q.Magnitude:F1} — {q.Place}\n" +
                        $"🕐 {q.UtcTime:MMM d, yyyy HH:mm} UTC\n" +
                        $"📏 Depth: {depth}" +
                        tsunami +
                        $"\n\nSource: USGS";
        var tagLine   = BlueskyPostHelper.HashtagLine(tags);

        var reserved   = Graphemes(headline) + Graphemes(dataBlock) + Graphemes(tagLine);
        var bodyBudget = Limit - reserved;

        var bodyBlock = "";
        if (body.Length > 0 && bodyBudget > 2)
        {
            var trimmed = TrimToGraphemes(body, bodyBudget - 2);
            if (trimmed.Length > 0)
                bodyBlock = $"\n\n{trimmed}";
        }

        return headline + bodyBlock + dataBlock + tagLine;
    }

    internal static string BuildMapAltText(QuakeEvent epicenter, IReadOnlyList<QuakeEvent> nearby)
    {
        var others = nearby.Where(q => q.Id != epicenter.Id).ToList();
        var depth  = epicenter.DepthKm.HasValue ? $"{epicenter.DepthKm.Value:F1} km" : "unknown depth";
        var lat    = epicenter.Latitude.GetValueOrDefault();
        var lon    = epicenter.Longitude.GetValueOrDefault();

        var contextLine = others.Count == 0
            ? "No other M3+ quakes in the surrounding 500 km in the past 24 hours."
            : $"In the surrounding 500 km over the past 24 hours, " +
              $"{others.Count} other M3+ quake{(others.Count == 1 ? "" : "s")} are shown as gray dots, " +
              $"ranging from M{others.Min(q => q.Magnitude):F1} to M{others.Max(q => q.Magnitude):F1}.";

        return
            $"Regional context map showing the epicenter of an M{epicenter.Magnitude:F1} earthquake near " +
            $"{epicenter.Place} at {lat:F2}°, {lon:F2}°, {depth}. " +
            $"The epicenter is marked by a red circle in the center of the map. " +
            contextLine + " " +
            "Map tiles by CARTO using OpenStreetMap data. Earthquake data from USGS.";
    }

    private static int Graphemes(string s) =>
        new System.Globalization.StringInfo(s).LengthInTextElements;

    private static string TrimToGraphemes(string s, int max)
    {
        var info = new System.Globalization.StringInfo(s);
        if (info.LengthInTextElements <= max) return s;
        var cut = info.SubstringByTextElements(0, max);
        var lastSpace = cut.LastIndexOf(' ');
        return lastSpace > 0 ? cut[..lastSpace] : cut;
    }
}
