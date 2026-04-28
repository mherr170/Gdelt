namespace GdeltSearchUI;

internal partial class CommodityForm : DataForm
{
    // Parallel arrays indexed to their respective catalog.
    private Label[]  _priceLabels         = null!;   // EIA — CommodityApiClient.Catalog
    private Label[]  _deltaLabels         = null!;
    private Label[]  _oilPriceLabels      = null!;   // Yahoo Finance — YahooFinanceApiClient.Catalog
    private Label[]  _oilPriceDeltaLabels = null!;
    // Per-card status labels (data freshness); _statusLabel = Bluesky feedback only.
    private Label    _eiaStatusLabel      = null!;
    private Label    _yahooStatusLabel    = null!;
    private Label    _statusLabel         = null!;
    // Per-card buttons — each set applies only to its source.
    private Button   _eiaRefreshButton    = null!;
    private Button   _yahooRefreshButton  = null!;
    private Button   _postButton          = null!;

    private CommodityData? _lastResult;
    private readonly BlueskyPoster _poster = new();

    public CommodityForm()
    {
        Text            = "Energy Spot Prices — EIA + Yahoo Finance";
        Size            = new Size(620, 660);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox     = false;
        StartPosition   = FormStartPosition.CenterScreen;
        Font            = new Font("Segoe UI", 9.5f);
        BackColor       = DarkTheme.Background;

        Controls.Add(BuildDataArea());
        Controls.Add(BuildStatusLabel());   // Bluesky/operational feedback at bottom

        Shown += async (_, _) => await FetchAllAsync();
    }

    // ── Formatters ────────────────────────────────────────────────────────────

    private static string FmtPrice(CommodityPrice p) => p.Slug switch
    {
        "gold"        => $"${p.Price:N1}",
        "natural_gas" => $"${p.Price:F3}",
        "copper"      => $"${p.Price:F3}",
        _             => $"${p.Price:F2}",
    };

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

    // ── Busy helpers (per-source) ─────────────────────────────────────────────

    private void SetEiaBusy(bool busy)
    {
        _eiaRefreshButton.Enabled = !busy;
        _eiaRefreshButton.Text    = busy ? "…" : "Refresh";
    }

    private void SetYahooBusy(bool busy)
    {
        _yahooRefreshButton.Enabled = !busy;
        _yahooRefreshButton.Text    = busy ? "…" : "Refresh";
    }

    protected override void SetStatus(string msg) => _statusLabel.Text = msg;

    // ── Post button ───────────────────────────────────────────────────────────

    internal void UpdatePostButton()
    {
        if (_lastResult is null) { _postButton.Enabled = false; return; }
        var today  = DateTime.Today.ToString("yyyy-MM-dd");
        var posted = CommodityPostTracker.HasBeenPosted(today);
        _postButton.Text      = posted ? $"✓ Posted {today}" : $"Post {today}";
        _postButton.BackColor = posted ? DarkTheme.PostButtonPosted : DarkTheme.PostButtonDefault;
        _postButton.Enabled   = true;
    }

    // ── Clear helpers (per-source + combined) ─────────────────────────────────

    private void ClearEiaPrices()
    {
        _lastResult           = null;
        _postButton.Enabled   = false;
        _postButton.Text      = "Post";
        _postButton.BackColor = DarkTheme.PostButtonDefault;
        for (var i = 0; i < _priceLabels.Length; i++)
        {
            _priceLabels[i].Text      = "—";
            _deltaLabels[i].Text      = "";
            _deltaLabels[i].ForeColor = DarkTheme.TextMuted;
        }
        _eiaStatusLabel.Text = "—";
    }

    private void ClearYahooPrices()
    {
        for (var i = 0; i < _oilPriceLabels.Length; i++)
        {
            _oilPriceLabels[i].Text         = "—";
            _oilPriceDeltaLabels[i].Text    = "";
            _oilPriceDeltaLabels[i].ForeColor = DarkTheme.TextMuted;
        }
        _yahooStatusLabel.Text = "—";
    }

    private void ClearAllPrices()
    {
        ClearEiaPrices();
        ClearYahooPrices();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _poster.Dispose();
        base.Dispose(disposing);
    }
}
