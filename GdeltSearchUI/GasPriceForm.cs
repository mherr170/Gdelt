namespace GdeltSearchUI;

internal partial class GasPriceForm : DataForm
{
    private Button           _refreshButton = null!;
    private Button           _postButton    = null!;
    private Label            _regularLabel  = null!;
    private Label            _midGradeLabel = null!;
    private Label            _premiumLabel  = null!;
    private Label            _dieselLabel   = null!;
    private Label            _regularDelta  = null!;
    private Label            _midGradeDelta = null!;
    private Label            _premiumDelta  = null!;
    private Label            _dieselDelta   = null!;
    private Label            _statusLabel   = null!;
    private NationalGasPrices? _lastResult;

    private readonly BlueskyPoster _poster = new();

    public GasPriceForm()
    {
        Text = "US Gas Prices — National Average";
        Size = new Size(580, 330);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9.5f);
        BackColor = DarkTheme.Background;

        Controls.Add(BuildPricePanel());
        Controls.Add(BuildToolbar());
        Controls.Add(BuildStatusLabel());

        Shown += async (_, _) => await FetchAsync();
    }

    private static string Fmt(double? v) => v.HasValue ? $"${v.Value:F3}" : "N/A";

    private static (string text, Color color) FormatDelta(double? curr, double? prev)
    {
        if (!curr.HasValue || !prev.HasValue) return ("", DarkTheme.TextMuted);
        var d = curr.Value - prev.Value;
        if (Math.Abs(d) < 0.0005) return ("→ 0.000", DarkTheme.TextMuted);
        var arrow = d > 0 ? "↑" : "↓";
        var color = d > 0 ? DarkTheme.DeltaUp : DarkTheme.DeltaDown;
        return ($"{arrow} {(d > 0 ? "+" : "-")}{Math.Abs(d):F3}", color);
    }

    private static void ApplyDelta(Label label, double? curr, double? prev)
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
        if (_lastResult is null) { _postButton.Enabled = false; return; }

        var posted = GasPricePostTracker.HasBeenPosted(_lastResult.Period);
        _postButton.Text      = posted ? $"✓ Posted {_lastResult.Period}" : $"Post  {_lastResult.Period}";
        _postButton.BackColor = posted ? DarkTheme.PostButtonPosted : DarkTheme.PostButtonDefault;
        _postButton.Enabled   = true;
    }

    private void ClearPrices()
    {
        _lastResult         = null;
        _postButton.Enabled = false;
        _postButton.Text      = "Post";
        _postButton.BackColor = DarkTheme.PostButtonDefault;
        _regularLabel.Text  = "—";
        _midGradeLabel.Text = "—";
        _premiumLabel.Text  = "—";
        _dieselLabel.Text   = "—";
        _regularDelta.Text  = "";
        _midGradeDelta.Text = "";
        _premiumDelta.Text  = "";
        _dieselDelta.Text   = "";
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _poster.Dispose();
        base.Dispose(disposing);
    }
}
