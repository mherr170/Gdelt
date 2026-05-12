namespace GdeltSearchUI;

internal partial class CongressForm
{
    private async Task PostToBlueskyAsync()
    {
        if (_grid.CurrentRow?.Tag is not CongressVote vote) return;

        var creds = CredentialManager.LoadCongressBluesky();
        if (creds is null)
        {
            using var setup = new SettingsDialog(
                CredentialManager.LoadCongressBluesky,
                CredentialManager.SaveCongressBluesky,
                "Bluesky Account — Congress Votes");
            if (setup.ShowDialog(this) != DialogResult.OK) return;
            creds = CredentialManager.LoadCongressBluesky();
            if (creds is null) return;
        }

        _postBtn.Enabled = false;
        SetStatus("Generating caption…");

        string headline; string[] tags;
        try   { (headline, tags) = await LmStudioPostGenerator.GenerateCongressPostAsync(vote); }
        catch { headline = vote.DisplayBill; tags = ["CongressVotes"]; }

        var text = CongressAutoPost.BuildPostText(vote, headline, tags);

        SetStatus("Posting to Bluesky…");
        var (ok, error) = await _poster.PostTextAsync(
            creds.Value.Handle, creds.Value.Password, text, CancellationToken.None);

        if (ok)
        {
            CongressPostTracker.MarkPosted(vote.UniqueKey);
            SetStatus($"Posted to Bluesky at {DateTime.Now:HH:mm}.");

            // Dim the posted row and update the button
            if (_grid.CurrentRow is { } row)
                row.DefaultCellStyle.ForeColor = DarkTheme.TextMuted;
        }
        else
        {
            ShowError(error!);
            SetStatus("Post failed — see details.");
        }

        UpdatePostButton();
    }
}
