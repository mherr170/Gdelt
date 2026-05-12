namespace GdeltSearchUI;

internal partial class ApodForm : DataForm
{
    private Button      _refreshBtn  = null!;
    private Button      _postBtn     = null!;
    private Label       _titleLabel  = null!;
    private Label       _dateLabel   = null!;
    private Label       _creditLabel = null!;
    private Label       _typeLabel   = null!;
    private RichTextBox _explanationBox = null!;
    private PictureBox  _pictureBox  = null!;
    private Label       _statusLabel = null!;

    private readonly BlueskyPoster _poster = new();
    private ApodEntry? _current;

    public ApodForm()
    {
        Text            = "NASA Astronomy Picture of the Day";
        Size            = new Size(820, 520);
        MinimumSize     = new Size(640, 420);
        StartPosition   = FormStartPosition.CenterScreen;
        Font            = new Font("Segoe UI", 9.5f);
        BackColor       = DarkTheme.Background;
        FormBorderStyle = FormBorderStyle.Sizable;

        Controls.Add(BuildMainPanel());
        Controls.Add(BuildToolbar());
        Controls.Add(BuildStatusLabel());

        Shown += async (_, _) => await FetchAsync();
    }

    private void SetBusy(bool busy)
    {
        _refreshBtn.Enabled = !busy;
        _refreshBtn.Text    = busy ? "…" : "Refresh";
    }

    protected override void SetStatus(string msg) => _statusLabel.Text = msg;

    private void UpdatePostButton()
    {
        if (_current is null) { _postBtn.Enabled = false; return; }
        var posted = ApodPostTracker.HasBeenPosted(_current.Date);
        _postBtn.Enabled   = true;
        _postBtn.Text      = posted ? $"✓ Posted {_current.Date}" : $"Post  {_current.Date}";
        _postBtn.BackColor = posted ? DarkTheme.PostButtonPosted : DarkTheme.PostButtonDefault;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _poster.Dispose(); _pictureBox.Image?.Dispose(); }
        base.Dispose(disposing);
    }
}
