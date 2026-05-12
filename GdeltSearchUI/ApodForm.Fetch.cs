namespace GdeltSearchUI;

internal partial class ApodForm
{
    private async Task FetchAsync()
    {
        var apiKey = CredentialManager.LoadNasaApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            SetStatus("No NASA API key — click ⚙ API Key to configure.");
            return;
        }

        SetBusy(true);
        ClearDisplay();
        SetStatus("Fetching today's APOD from NASA…");

        ApodEntry? entry;
        try
        {
            using var client = new ApodApiClient(apiKey);
            entry = await client.GetTodayAsync();
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
            SetBusy(false);
            return;
        }

        if (entry is null)
        {
            SetStatus("No data returned from NASA API.");
            SetBusy(false);
            return;
        }

        _current = entry;

        _titleLabel.Text  = entry.Title;
        _dateLabel.Text   = entry.Date;
        _creditLabel.Text = entry.Copyright is not null ? $"© {entry.Copyright}" : "";
        _typeLabel.Text   = entry.IsVideo ? "🎬 Video (image thumbnail shown)" : "";
        _explanationBox.Text = entry.Explanation;

        if (!string.IsNullOrWhiteSpace(entry.ImageUrl))
        {
            SetStatus("Loading image…");
            await LoadImageAsync(entry.ImageUrl);
        }

        UpdatePostButton();
        SetStatus($"APOD for {entry.Date} — \"{entry.Title}\"");
        SetBusy(false);
    }

    private async Task LoadImageAsync(string url)
    {
        try
        {
            using var http  = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            var bytes = await http.GetByteArrayAsync(url);
            using var ms    = new MemoryStream(bytes);
            var image = System.Drawing.Image.FromStream(ms);

            var old = _pictureBox.Image;
            _pictureBox.Image = image;
            old?.Dispose();
        }
        catch
        {
            // Image load failed — leave placeholder, post will still work
        }
    }

    private void ClearDisplay()
    {
        _current              = null;
        _titleLabel.Text      = "—";
        _dateLabel.Text       = "";
        _creditLabel.Text     = "";
        _typeLabel.Text       = "";
        _explanationBox.Text  = "";
        var old = _pictureBox.Image;
        _pictureBox.Image     = null;
        old?.Dispose();
        _postBtn.Enabled      = false;
        _postBtn.Text         = "Post";
        _postBtn.BackColor    = DarkTheme.PostButtonDefault;
    }
}
