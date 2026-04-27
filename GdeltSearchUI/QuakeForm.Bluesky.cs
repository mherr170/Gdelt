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

        SetStatus("Posting to Bluesky…");

        var text = BuildPostText(quake, headline, body, tags);
        var (ok, error) = await _poster.PostTextAsync(
            creds.Value.Handle, creds.Value.Password, text, CancellationToken.None);

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
