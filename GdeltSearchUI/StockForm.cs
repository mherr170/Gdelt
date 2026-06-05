namespace GdeltSearchUI;

internal partial class StockForm : DataForm
{
    private Button      _refreshBtn  = null!;
    private Button      _postBtn     = null!;
    private DataGridView _grid       = null!;
    private PictureBox  _chartBox    = null!;
    private Label       _statusLabel = null!;

    private readonly BlueskyPoster _poster = new();
    private IReadOnlyList<StockEntry> _lastEntries = [];
    private string? _tradingDate;
    private MemoryStream? _chartStream;

    public StockForm()
    {
        Text            = "US Stock Market — Daily Close";
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
        if (_tradingDate is null) { _postBtn.Enabled = false; return; }
        var posted = StockPostTracker.HasBeenPosted(_tradingDate);
        _postBtn.Enabled   = true;
        _postBtn.Text      = posted ? $"✓ Posted {_tradingDate}" : $"Post  {_tradingDate}";
        _postBtn.BackColor = posted ? DarkTheme.PostButtonPosted : DarkTheme.PostButtonDefault;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _poster.Dispose(); _chartBox.Image?.Dispose(); _chartStream?.Dispose(); }
        base.Dispose(disposing);
    }
}
