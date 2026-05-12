namespace GdeltSearchUI;

internal partial class WeatherForm : DataForm
{
    private Button           _refreshBtn  = null!;
    private Button           _postBtn     = null!;
    private DataGridView     _grid        = null!;
    private ToolStripStatusLabel  _statusLabel = null!;
    private ToolStripProgressBar  _progress    = null!;

    private readonly BlueskyPoster _poster = new();
    private CancellationTokenSource? _cts;

    public WeatherForm()
    {
        Text            = "NOAA Severe Weather Alerts";
        Size            = new Size(900, 480);
        MinimumSize     = new Size(700, 360);
        StartPosition   = FormStartPosition.CenterScreen;
        Font            = new Font("Segoe UI", 9.5f);
        BackColor       = DarkTheme.Background;
        FormBorderStyle = FormBorderStyle.Sizable;

        Controls.Add(BuildGrid());
        Controls.Add(BuildToolbar());
        Controls.Add(BuildStatusBar());

        Shown += async (_, _) => await FetchAsync();
    }

    private void SetBusy(bool busy)
    {
        _refreshBtn.Enabled = !busy;
        _refreshBtn.Text    = busy ? "…" : "Refresh";
        _progress.Visible   = busy;
        if (busy) _progress.Style = ProgressBarStyle.Marquee;
    }

    protected override void SetStatus(string msg) => _statusLabel.Text = msg;

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _poster.Dispose(); _cts?.Dispose(); }
        base.Dispose(disposing);
    }
}
