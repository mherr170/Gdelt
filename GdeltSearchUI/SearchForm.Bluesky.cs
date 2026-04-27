namespace GdeltSearchUI;

public sealed partial class SearchForm
{
    private async Task PostToBlueskyAsync(string title, string url)
    {
        var creds = CredentialManager.Load();
        if (creds is null)
        {
            using var dlg = new SettingsDialog();
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            creds = CredentialManager.Load();
            if (creds is null) return;
        }

        SetStatus("Posting to Bluesky…");
        var (ok, error) = await _poster.PostAsync(creds.Value.Handle, creds.Value.Password, title, url, CancellationToken.None);

        if (ok)
            SetStatus($"Posted to Bluesky: {title[..Math.Min(title.Length, 60)]}…");
        else
        {
            SetStatus("Post failed — see details.");
            ErrorDialog.Show(this, error!);
        }
    }
}
