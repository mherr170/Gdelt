namespace GdeltSearchUI;

internal partial class CommodityForm : DataForm
{
    private Label[]  _oilPriceLabels      = null!;
    private Label[]  _oilPriceDeltaLabels = null!;
    private Label    _yahooStatusLabel    = null!;
    private Label    _statusLabel         = null!;
    private Button   _yahooRefreshButton  = null!;
    private Button   _yahooPostButton     = null!;

    private IReadOnlyList<OilPriceEntry>? _yahooData;
    private readonly BlueskyPoster _poster = new();

    public CommodityForm()
    {
        Text            = "Energy Futures — Yahoo Finance";
        Size            = new Size(620, 380);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox     = false;
        StartPosition   = FormStartPosition.CenterScreen;
        Font            = new Font("Segoe UI", 9.5f);
        BackColor       = DarkTheme.Background;

        Controls.Add(BuildDataArea());
        Controls.Add(BuildStatusLabel());

        Shown += async (_, _) => await FetchYahooAsync();
    }

    private static string FmtOilPrice(OilPriceEntry e) => e.Code switch
    {
        "NATURAL_GAS"                     => $"${e.Price:F3}",
        "RBOB_GASOLINE" or "HEATING_OIL" => $"${e.Price:F3}",
        _                                 => $"${e.Price:F2}",
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

    private void SetYahooBusy(bool busy)
    {
        _yahooRefreshButton.Enabled = !busy;
        _yahooRefreshButton.Text    = busy ? "…" : "Refresh";
    }

    protected override void SetStatus(string msg) => _statusLabel.Text = msg;

    internal void UpdateYahooPostButton()
    {
        if (_yahooData is null || _yahooData.Count == 0)
        {
            _yahooPostButton.Enabled = false;
            return;
        }
        var today  = DateTime.Today.ToString("yyyy-MM-dd");
        var posted = YahooPostTracker.HasBeenPosted(today);
        _yahooPostButton.Text      = posted ? $"✓ Posted {today}" : $"Post {today}";
        _yahooPostButton.BackColor = posted ? DarkTheme.PostButtonPosted : DarkTheme.PostButtonDefault;
        _yahooPostButton.Enabled   = true;
    }

    private void ClearYahooPrices()
    {
        _yahooData                 = null;
        _yahooPostButton.Enabled   = false;
        _yahooPostButton.Text      = "Post";
        _yahooPostButton.BackColor = DarkTheme.PostButtonDefault;
        for (var i = 0; i < _oilPriceLabels.Length; i++)
        {
            _oilPriceLabels[i].Text           = "—";
            _oilPriceDeltaLabels[i].Text      = "";
            _oilPriceDeltaLabels[i].ForeColor = DarkTheme.TextMuted;
        }
        _yahooStatusLabel.Text = "—";
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _poster.Dispose();
        base.Dispose(disposing);
    }
}
