namespace GdeltSearchUI;

internal partial class QuakeForm
{
    private async Task PostToBlueskyAsync()
    {
        if (_grid.CurrentRow?.Tag is not QuakeEvent quake) return;

        var creds = CredentialManager.LoadQuakeBluesky();
        if (creds is null)
        {
            using var setup = new SettingsDialog(
                CredentialManager.LoadQuakeBluesky,
                CredentialManager.SaveQuakeBluesky,
                "Bluesky Account — Earthquakes");
            if (setup.ShowDialog(this) != DialogResult.OK) return;
            creds = CredentialManager.LoadQuakeBluesky();
            if (creds is null) return;
        }

        _postBtn.Enabled = false;
        SetStatus("Generating caption…");

        var (headline, body, tags) = await LmStudioPostGenerator.GenerateQuakePostAsync(quake);

        var text = BuildPostText(quake, headline, body, tags);

        (bool ok, string? error) result;
        if (quake.Latitude.HasValue && quake.Longitude.HasValue)
        {
            SetStatus("Fetching nearby quakes for context map…");
            List<QuakeEvent> nearby;
            using (var client = new QuakeApiClient())
            {
                nearby = await client.GetNearbyAsync(
                    quake.Latitude.Value, quake.Longitude.Value,
                    radiusKm: 500, hours: 24, minMagnitude: 3.0);
            }

            SetStatus("Rendering regional map and posting to Bluesky…");
            var png = await QuakeMap.RenderPngAsync(quake, nearby);
            if (png.Length > 0)
            {
                var alt = BuildMapAltText(quake, nearby);
                result = await _poster.PostTextWithImageAsync(
                    creds.Value.Handle, creds.Value.Password, text, png, alt, CancellationToken.None);
            }
            else
            {
                result = await _poster.PostTextAsync(
                    creds.Value.Handle, creds.Value.Password, text, CancellationToken.None);
            }
        }
        else
        {
            SetStatus("Posting to Bluesky (text only — no coordinates)…");
            result = await _poster.PostTextAsync(
                creds.Value.Handle, creds.Value.Password, text, CancellationToken.None);
        }
        var (ok, error) = result;

        if (ok)
            SetStatus($"Posted to Bluesky at {DateTime.Now:HH:mm}.");
        else
        {
            ShowError(error!);
            SetStatus("Post failed — see details.");
        }

        _postBtn.Enabled = true;
    }

    private static string BuildPostText(QuakeEvent q, string headline, string body, string[] tags)
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

        // Reserve space for headline + data + tags; body fills whatever remains
        var reserved   = Graphemes(headline) + Graphemes(dataBlock) + Graphemes(tagLine);
        var bodyBudget = Limit - reserved;

        var bodyBlock = "";
        if (body.Length > 0 && bodyBudget > 2)
        {
            var trimmed = TrimToGraphemes(body, bodyBudget - 2); // -2 for the leading "\n\n"
            if (trimmed.Length > 0)
                bodyBlock = $"\n\n{trimmed}";
        }

        return headline + bodyBlock + dataBlock + tagLine;
    }

    private static string BuildMapAltText(QuakeEvent epicenter, IReadOnlyList<QuakeEvent> nearby)
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
