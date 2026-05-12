namespace GdeltSearchUI;

internal partial class WeatherForm
{
    private async Task PostToBlueskyAsync()
    {
        if (_grid.CurrentRow?.Tag is not WeatherAlert alert) return;

        var creds = CredentialManager.LoadWeatherBluesky();
        if (creds is null)
        {
            using var setup = new SettingsDialog(
                CredentialManager.LoadWeatherBluesky,
                CredentialManager.SaveWeatherBluesky,
                "Bluesky Account — Severe Weather");
            if (setup.ShowDialog(this) != DialogResult.OK) return;
            creds = CredentialManager.LoadWeatherBluesky();
            if (creds is null) return;
        }

        _postBtn.Enabled = false;
        SetStatus("Generating caption…");

        string headline; string[] tags;
        try   { (headline, tags) = await LmStudioPostGenerator.GenerateWeatherAlertPostAsync(alert); }
        catch { headline = alert.Headline.Length > 0 ? alert.Headline : alert.Event; tags = ["WeatherAlert"]; }

        var text = WeatherAutoPost.BuildPostText(alert, headline, tags);

        SetStatus("Posting to Bluesky…");
        var (ok, error) = await _poster.PostTextAsync(
            creds.Value.Handle, creds.Value.Password, text, CancellationToken.None);

        if (ok)
        {
            WeatherPostTracker.MarkPosted(alert.Id);
            SetStatus($"Posted to Bluesky at {DateTime.Now:HH:mm}.");
            if (_grid.CurrentRow is { } row)
                row.DefaultCellStyle.ForeColor = DarkTheme.TextMuted;
        }
        else
        {
            ShowError(error!);
            SetStatus("Post failed — see details.");
        }

        // Refresh post button state
        _grid.CurrentRow?.Tag?.GetType(); // trigger SelectionChanged
        if (_grid.CurrentRow?.Tag is WeatherAlert a)
        {
            var posted = WeatherPostTracker.HasBeenPosted(a.Id);
            _postBtn.Enabled   = true;
            _postBtn.Text      = posted ? "✓ Posted" : "Post Selected";
            _postBtn.BackColor = posted ? DarkTheme.PostButtonPosted : DarkTheme.PostButtonDefault;
        }
    }
}
