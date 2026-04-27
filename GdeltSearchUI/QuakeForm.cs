namespace GdeltSearchUI;

internal partial class QuakeForm : DataForm
{
    private static readonly (string Label, double MinMag)[] MagFilters =
    [
        ("M 2.5+", 2.5),
        ("M 4.5+", 4.5),
        ("M 5.0+", 5.0),
        ("M 6.0+", 6.0),
        ("M 7.0+", 7.0),
    ];

    private static readonly (string Label, int Hours)[] TimeRanges =
    [
        ("Past 24 h",  24),
        ("Past 7 days", 168),
        ("Past 30 days", 720),
    ];

    private ComboBox    _magBox       = null!;
    private ComboBox    _timeBox      = null!;
    private Button      _refreshBtn   = null!;
    private Button      _postBtn      = null!;
    private CheckBox    _autoPostCheck = null!;
    private DataGridView _grid        = null!;
    private ToolStripStatusLabel _statusLabel = null!;
    private ToolStripProgressBar _progress   = null!;

    private readonly BlueskyPoster _poster = new();
    private readonly System.Windows.Forms.Timer _autoTimer = new() { Interval = 10 * 60 * 1000 };
    private CancellationTokenSource? _cts;

    public QuakeForm()
    {
        Text = "USGS Earthquake Feed";
        Size = new Size(740, 460);
        MinimumSize = new Size(560, 340);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9.5f);
        BackColor = DarkTheme.Background;

        Controls.Add(BuildGrid());
        Controls.Add(BuildToolbar());
        Controls.Add(BuildStatusBar());

        _autoTimer.Tick += async (_, _) => await AutoPostAsync();

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
        if (disposing) { _poster.Dispose(); _cts?.Dispose(); _autoTimer.Dispose(); }
        base.Dispose(disposing);
    }
}
