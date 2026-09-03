namespace GdeltSearchUI;

internal partial class DebtForm
{
    private async Task PostToBlueskyAsync()
    {
        if (_lastResult?.Current is null) return;

        var creds = CredentialManager.LoadDebtBluesky();
        if (creds is null)
        {
            using var setup = new SettingsDialog(
                CredentialManager.LoadDebtBluesky,
                CredentialManager.SaveDebtBluesky,
                "Bluesky Account — National Debt");
            if (setup.ShowDialog(this) != DialogResult.OK) return;
            creds = CredentialManager.LoadDebtBluesky();
            if (creds is null) return;
        }

        _postButton.Enabled = false;
        SetStatus("Generating caption…");

        var (headline, tags) = await LmStudioPostGenerator.GenerateDebtPostAsync(_lastResult);

        SetStatus("Fetching debt history for chart…");
        List<DebtSnapshot> history;
        using (var client = new DebtApiClient())
            history = await client.GetHistorySinceAsync(DebtApiClient.HistoryStart);

        var text = BuildPostText(_lastResult, headline, tags);
        (bool ok, string? error) result;

        if (history.Count >= 2)
        {
            SetStatus("Rendering chart and posting to Bluesky…");
            var png = DebtSparkline.RenderPng(history);
            var alt = BuildAltText(history);
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
            DebtPostTracker.MarkPosted(_lastResult.Current.RecordDate.ToString("yyyy-MM-dd"));
            SetStatus($"Posted to Bluesky at {DateTime.Now:HH:mm}.");
        }
        else
        {
            ShowError(error!);
            SetStatus("Post failed — see details.");
        }

        UpdatePostButton();
    }

    private static string BuildPostText(NationalDebt d, string headline, string[] tags) =>
        DebtAutoPost.BuildPostText(d, headline, tags);

    private static string BuildAltText(IReadOnlyList<DebtSnapshot> history) =>
        DebtAutoPost.BuildAltText(history);
}
