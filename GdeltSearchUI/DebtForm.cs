namespace GdeltSearchUI;

internal partial class DebtForm : DataForm
{
    private Button _refreshButton = null!;
    private Button _postButton    = null!;
    private Label  _totalLabel    = null!;
    private Label  _publicLabel   = null!;
    private Label  _intragovLabel = null!;
    private Label  _percentLabel  = null!;
    private Label  _totalDelta    = null!;
    private Label  _publicDelta   = null!;
    private Label  _intragovDelta = null!;
    private Label  _percentDelta  = null!;
    private Label  _statusLabel   = null!;
    private NationalDebt? _lastResult;

    private readonly BlueskyPoster _poster = new();

    public DebtForm()
    {
        Text = "US National Debt — Debt to the Penny";
        Size = new Size(620, 360);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9.5f);
        BackColor = DarkTheme.Background;

        Controls.Add(BuildDebtPanel());
        Controls.Add(BuildToolbar());
        Controls.Add(BuildStatusLabel());

        Shown += async (_, _) => await FetchAsync();
    }

    private static string FmtTrillions(decimal v) =>
        $"${(v / 1_000_000_000_000m):F3}T";

    private static string FmtBillionsDelta(decimal v)
    {
        var b = v / 1_000_000_000m;
        var sign = b >= 0 ? "+" : "-";
        return $"{sign}${Math.Abs(b):F2}B";
    }

    private static (string text, Color color) FormatDelta(decimal? curr, decimal? prev)
    {
        if (!curr.HasValue || !prev.HasValue) return ("", DarkTheme.TextMuted);
        var d = curr.Value - prev.Value;
        if (d == 0m) return ("→ 0.00B", DarkTheme.TextMuted);
        var arrow = d > 0 ? "↑" : "↓";
        var color = d > 0 ? DarkTheme.DeltaUp : DarkTheme.DeltaDown;
        return ($"{arrow} {FmtBillionsDelta(d)}", color);
    }

    private static void ApplyDelta(Label label, decimal? curr, decimal? prev)
    {
        var (text, color) = FormatDelta(curr, prev);
        label.Text = text;
        label.ForeColor = color;
    }

    private void SetBusy(bool busy)
    {
        _refreshButton.Enabled = !busy;
        _refreshButton.Text    = busy ? "…" : "Refresh";
    }

    protected override void SetStatus(string msg) => _statusLabel.Text = msg;

    internal void UpdatePostButton()
    {
        if (_lastResult?.Current is null) { _postButton.Enabled = false; return; }
        var date = _lastResult.Current.RecordDate.ToString("yyyy-MM-dd");
        var posted = DebtPostTracker.HasBeenPosted(date);
        _postButton.Text      = posted ? $"✓ Posted {date}" : $"Post  {date}";
        _postButton.BackColor = posted ? DarkTheme.PostButtonPosted : DarkTheme.PostButtonDefault;
        _postButton.Enabled   = true;
    }

    private void ClearValues()
    {
        _lastResult         = null;
        _postButton.Enabled = false;
        _postButton.Text      = "Post";
        _postButton.BackColor = DarkTheme.PostButtonDefault;
        _totalLabel.Text    = "—";
        _publicLabel.Text   = "—";
        _intragovLabel.Text = "—";
        _percentLabel.Text  = "—";
        _totalDelta.Text    = "";
        _publicDelta.Text   = "";
        _intragovDelta.Text = "";
        _percentDelta.Text  = "";
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _poster.Dispose();
        base.Dispose(disposing);
    }
}
