namespace GdeltSearchUI;

internal partial class GasPriceForm
{
    private async Task PostToBlueskyAsync()
    {
        if (_lastResult is null) return;

        var creds = CredentialManager.LoadGasPriceBluesky();
        if (creds is null)
        {
            using var setup = new SettingsDialog(
                CredentialManager.LoadGasPriceBluesky,
                CredentialManager.SaveGasPriceBluesky,
                "Bluesky Account — Gas Prices");
            if (setup.ShowDialog(this) != DialogResult.OK) return;
            creds = CredentialManager.LoadGasPriceBluesky();
            if (creds is null) return;
        }

        _postButton.Enabled = false;
        SetStatus("Generating caption…");

        var (headline, tags) = await LmStudioPostGenerator.GenerateGasPricePostAsync(_lastResult);

        var text = GasAutoPost.BuildPostText(_lastResult, headline, tags);
        (bool ok, string? error) result;

        const int MonthsToShow = 3;
        var monthly = GasPriceChart.ComputeMonthlyAverages(_lastResult.History);
        if (monthly.Count > MonthsToShow) monthly = monthly.TakeLast(MonthsToShow).ToList();
        if (monthly.Count >= 2)
        {
            SetStatus("Rendering monthly-averages chart and posting to Bluesky…");
            var png = GasPriceChart.RenderMonthlyAveragesPng(_lastResult.History, MonthsToShow);
            var alt = GasAutoPost.BuildAltText(monthly);
            result = await _poster.PostTextWithImageAsync(
                creds.Value.Handle, creds.Value.Password, text, png, alt, CancellationToken.None);
        }
        else
        {
            SetStatus("Posting to Bluesky (text only — insufficient history)…");
            result = await _poster.PostTextAsync(
                creds.Value.Handle, creds.Value.Password, text, CancellationToken.None);
        }
        var (ok, error) = result;

        if (ok)
        {
            GasPricePostTracker.MarkPosted(_lastResult.Period);
            SetStatus($"Posted to Bluesky at {DateTime.Now:HH:mm}.");
        }
        else
        {
            ShowError(error!);
            SetStatus("Post failed — see details.");
        }

        UpdatePostButton();
    }

}
