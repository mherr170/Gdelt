namespace GdeltSearchUI;

internal partial class CommodityForm
{
    private async Task PostYahooToBlueskyAsync()
    {
        if (_yahooData is null || _yahooData.Count == 0) return;

        var creds = CredentialManager.LoadYahooBluesky();
        if (creds is null)
        {
            using var setup = new SettingsDialog(
                CredentialManager.LoadYahooBluesky,
                CredentialManager.SaveYahooBluesky,
                "Bluesky Account — Yahoo Finance Futures");
            if (setup.ShowDialog(this) != DialogResult.OK) return;
            creds = CredentialManager.LoadYahooBluesky();
            if (creds is null) return;
        }

        _yahooPostButton.Enabled = false;
        SetStatus("Generating caption…");

        var savedHistory   = YahooPriceCache.Load();
        var lastPostPrices = savedHistory.LastOrDefault()?.Prices;

        var (headline, tags) = await LmStudioPostGenerator.GenerateYahooFuturesPostAsync(_yahooData, lastPostPrices);
        var text = YahooAutoPost.BuildPostText(_yahooData, headline, tags, lastPostPrices);

        AppLogger.Log($"Yahoo Bluesky: posting as '{creds.Value.Handle}'");
        var previewHistory = savedHistory.Append(new CommodityHistoryPoint
        {
            Timestamp = DateTimeOffset.Now,
            Prices    = _yahooData.ToDictionary(e => e.Code, e => e.Price),
        }).ToList();

        var png = await Task.Run(() => YahooChartGenerator.RenderPng(previewHistory));
        (bool ok, string? error) result;

        if (png.Length > 0)
        {
            SetStatus("Rendering chart and posting to Bluesky…");
            var alt = YahooAutoPost.BuildAltText(_yahooData, previewHistory);
            result = await _poster.PostTextWithImageAsync(
                creds.Value.Handle, creds.Value.Password, text, png, alt, CancellationToken.None);
        }
        else
        {
            SetStatus("Posting Yahoo futures to Bluesky (text only)…");
            result = await _poster.PostTextAsync(
                creds.Value.Handle, creds.Value.Password, text, CancellationToken.None);
        }

        var (ok, error) = result;
        if (ok)
        {
            YahooPriceCache.Append(_yahooData);
            YahooPostTracker.MarkPosted(DateTime.Today.ToString("yyyy-MM-dd"));
            SetStatus($"Posted Yahoo futures to Bluesky at {DateTime.Now:HH:mm}.");
        }
        else
        {
            ShowError(error!);
            SetStatus("Yahoo post failed — see details.");
        }

        UpdateYahooPostButton();
    }
}
