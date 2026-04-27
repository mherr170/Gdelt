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

        SetStatus("Posting to Bluesky…");

        var text = BuildPostText(_lastResult, headline, tags);
        var (ok, error) = await _poster.PostTextAsync(
            creds.Value.Handle, creds.Value.Password, text, CancellationToken.None);

        if (ok)
            SetStatus($"Posted to Bluesky at {DateTime.Now:HH:mm}.");
        else
        {
            ShowError(error!);
            SetStatus("Post failed — see details.");
        }

        _postButton.Enabled = true;
    }

    private static string BuildPostText(NationalGasPrices p, string headline, string[] tags)
    {
        var hashtagLine = BlueskyPostHelper.HashtagLine(tags);
        return $"{headline}\n\n" +
               $"⛽ Week of {p.Period} ⛽\n\n" +
               $"Regular:   {Bold(Fmt(p.Regular))}/gal\n" +
               $"Mid-Grade: {Bold(Fmt(p.MidGrade))}/gal\n" +
               $"Premium:   {Bold(Fmt(p.Premium))}/gal\n" +
               $"Diesel:    {Bold(Fmt(p.Diesel))}/gal\n\n" +
               $"Source: EIA{hashtagLine}";
    }

    // Converts digits to Unicode mathematical bold digits (U+1D7CE–U+1D7D7),
    // the only reliable way to render bold in Bluesky posts.
    private static string Bold(string s)
    {
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (var c in s)
            sb.Append(c is >= '0' and <= '9' ? char.ConvertFromUtf32(0x1D7CE + (c - '0')) : c.ToString());
        return sb.ToString();
    }
}
