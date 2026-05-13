namespace GdeltSearchUI;

internal partial class ApodForm
{
    private async Task PostToBlueskyAsync()
    {
        if (_current is null) return;

        var creds = CredentialManager.LoadApodBluesky();
        if (creds is null)
        {
            using var setup = new SettingsDialog(
                CredentialManager.LoadApodBluesky,
                CredentialManager.SaveApodBluesky,
                "Bluesky Account — NASA APOD");
            if (setup.ShowDialog(this) != DialogResult.OK) return;
            creds = CredentialManager.LoadApodBluesky();
            if (creds is null) return;
        }

        _postBtn.Enabled = false;
        try
        {
            SetStatus("Generating caption…");

            string headline; string[] tags;
            try   { (headline, tags) = await LmStudioPostGenerator.GenerateApodPostAsync(_current); }
            catch { headline = _current.Title; tags = ["Astrophotography", "Space"]; }

            var text = ApodAutoPost.BuildPostText(_current, headline, tags);

            SetStatus("Posting to Bluesky…");

            (bool ok, string? error) result;
            if (!string.IsNullOrWhiteSpace(_current.ImageUrl))
            {
                var png = await ApodAutoPost.TryDownloadImageAsync(_current.ImageUrl, CancellationToken.None);
                if (png.Length > 0)
                {
                    var alt = $"NASA Astronomy Picture of the Day for {_current.Date}: {_current.Title}";
                    result  = await _poster.PostTextWithImageAsync(
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
                result = await _poster.PostTextAsync(
                    creds.Value.Handle, creds.Value.Password, text, CancellationToken.None);
            }

            if (result.ok)
            {
                ApodPostTracker.MarkPosted(_current.Date);
                SetStatus($"Posted to Bluesky at {DateTime.Now:HH:mm}.");
            }
            else
            {
                ShowError(result.error!);
                SetStatus("Post failed — see details.");
            }
        }
        finally
        {
            UpdatePostButton();
        }
    }


}
