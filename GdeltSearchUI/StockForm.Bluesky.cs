namespace GdeltSearchUI;

internal partial class StockForm
{
    private async Task PostToBlueskyAsync()
    {
        if (_lastEntries.Count == 0 || _tradingDate is null) return;

        var creds = CredentialManager.LoadStockBluesky();
        if (creds is null)
        {
            using var setup = new SettingsDialog(
                CredentialManager.LoadStockBluesky,
                CredentialManager.SaveStockBluesky,
                "Bluesky Account — Stock Market");
            if (setup.ShowDialog(this) != DialogResult.OK) return;
            creds = CredentialManager.LoadStockBluesky();
            if (creds is null) return;
        }

        _postBtn.Enabled = false;
        SetStatus("Generating caption…");

        string headline; string[] tags;
        try   { (headline, tags) = await LmStudioPostGenerator.GenerateStockPostAsync(_lastEntries); }
        catch { headline = $"US market close — {_tradingDate}"; tags = ["StockMarket", "WallStreet"]; }

        var text = StockAutoPost.BuildPostText(_lastEntries, _tradingDate, headline, tags);

        SetStatus("Rendering chart…");
        byte[] png;
        try   { png = await Task.Run(() => StockChartGenerator.RenderPng(_lastEntries)); }
        catch { png = []; }

        SetStatus("Posting to Bluesky…");
        (bool ok, string? error) result;
        if (png.Length > 0)
        {
            var alt = StockAutoPost.BuildAltText(_lastEntries, _tradingDate);
            result  = await _poster.PostTextWithImageAsync(
                creds.Value.Handle, creds.Value.Password, text, png, alt, CancellationToken.None);
        }
        else
        {
            result = await _poster.PostTextAsync(
                creds.Value.Handle, creds.Value.Password, text, CancellationToken.None);
        }

        if (result.ok)
        {
            StockPostTracker.MarkPosted(_tradingDate);
            SetStatus($"Posted to Bluesky at {DateTime.Now:HH:mm}.");
        }
        else
        {
            ShowError(result.error!);
            SetStatus("Post failed — see details.");
        }

        UpdatePostButton();
    }
}
