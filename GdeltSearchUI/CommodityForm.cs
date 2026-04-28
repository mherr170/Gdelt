namespace GdeltSearchUI;

internal partial class CommodityForm : DataForm
{
    // Parallel arrays, indexed to match CommodityApiClient.Catalog order.
    private Label[]  _priceLabels   = null!;
    private Label[]  _deltaLabels   = null!;
    private Label    _statusLabel   = null!;
    private Button   _refreshButton = null!;
    private Button   _postButton    = null!;
    private CommodityData? _lastResult;

    private readonly BlueskyPoster _poster = new();

    public CommodityForm()
    {
        Text = "Energy Spot Prices — EIA";
        Size = new Size(580, 310);
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

    private static string FmtPrice(CommodityPrice p) => p.Slug switch
    {
        "gold"        => $"${p.Price:N1}",
        "natural_gas" => $"${p.Price:F3}",
        "copper"      => $"${p.Price:F3}",
        _             => $"${p.Price:F2}",
    };

    private static (string text, Color color) FormatDelta(double curr, double? prev)
    {
        if (!prev.HasValue || prev.Value == 0) return ("", DarkTheme.TextMuted);
        var pct = (curr - prev.Value) / prev.Value * 100.0;
        if (Math.Abs(pct) < 0.005) return ("➖ 0.00%", DarkTheme.TextMuted);
        var arrow = pct > 0 ? "↑" : "↓";
        var color = pct > 0 ? DarkTheme.DeltaUp : DarkTheme.DeltaDown;
        return ($"{arrow} {(pct > 0 ? "+" : "")}{pct:F2}%", color);
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
        var today  = DateTime.Today.ToString("yyyy-MM-dd");
        var posted = CommodityPostTracker.HasBeenPosted(today);
        _postButton.Text      = posted ? $"✓ Posted {today}" : $"Post {today}";
        _postButton.BackColor = posted ? DarkTheme.PostButtonPosted : DarkTheme.PostButtonDefault;
        _postButton.Enabled   = true;
    }

    private void ClearPrices()
    {
        _lastResult         = null;
        _postButton.Enabled = false;
        _postButton.Text      = "Post";
        _postButton.BackColor = DarkTheme.PostButtonDefault;
        for (var i = 0; i < _priceLabels.Length; i++)
        {
            _priceLabels[i].Text      = "—";
            _deltaLabels[i].Text      = "";
            _deltaLabels[i].ForeColor = DarkTheme.TextMuted;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _poster.Dispose();
        base.Dispose(disposing);
    }
}
