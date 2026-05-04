namespace GdeltSearchUI;

internal sealed class LauncherForm : Form
{
    private static readonly (string Label, string Query, int TimespanIndex)[] SearchPresets =
    [
        ("USGunV", "shooting", 1),
    ];

    // Buttons whose color is managed by auto-post / refresh methods
    private Button _gasPriceBtn  = null!;
    private Button _debtBtn      = null!;
    private Button _commodityBtn = null!;

    // Dynamic stat labels
    private Label _debtStat   = null!;
    private Label _gasStat    = null!;
    private Label _energyStat = null!;

    // Debt auto-post infrastructure
    private readonly CancellationTokenSource    _cts       = new();
    private readonly System.Windows.Forms.Timer _debtTimer = new() { Interval = 3 * 60 * 60 * 1000 };
    private bool _debtPostInFlight;

    // Yahoo auto-post infrastructure
    private readonly System.Windows.Forms.Timer _yahooTimer = new() { Interval = 8 * 60 * 60 * 1000 };
    private bool _yahooPostInFlight;

    public LauncherForm()
    {
        Text            = "GDELT Dashboard";
        Size            = new Size(540, 280);
        MinimumSize     = new Size(400, 220);
        StartPosition   = FormStartPosition.CenterScreen;
        Font            = new Font("Segoe UI", 9.5f);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox     = false;
        BackColor       = DarkTheme.Background;

        Controls.Add(BuildDashboard());
        Controls.Add(BuildHeader());

        RefreshGasPriceButton();

        // Debt and Energy owned by auto-post — neutral pending state until API responds
        _debtBtn.Text       = "US Debt";
        _debtBtn.BackColor  = DarkTheme.Raised;
        _debtStat.Text      = "Checking…";
        _debtStat.ForeColor = DarkTheme.TextMuted;

        _commodityBtn.Text       = "Energy $";
        _commodityBtn.BackColor  = DarkTheme.Raised;
        _energyStat.Text         = "Checking…";
        _energyStat.ForeColor    = DarkTheme.TextMuted;

        FormClosing += (_, _) =>
        {
            _cts.Cancel();
            _debtTimer.Stop();
            _yahooTimer.Stop();
        };

        _debtTimer.Tick  += (_, _) => _ = SafeDebtPostAsync();
        _yahooTimer.Tick += (_, _) => _ = SafeYahooPostAsync();

        _debtTimer.Start();
        _yahooTimer.Start();

        _ = SafeDebtPostAsync();
        _ = SafeYahooPostAsync();
    }

    // ── Debt auto-post ───────────────────────────────────────────────────────────

    private async Task SafeDebtPostAsync()
    {
        if (_debtPostInFlight) return;
        _debtPostInFlight = true;
        try
        {
            DebtAutoPostResult result;
            try   { result = await DebtAutoPost.PostIfNeededAsync(_cts.Token); }
            catch (OperationCanceledException) { return; }
            catch (Exception ex) { result = new(DebtAutoPostOutcome.Failed, ErrorMessage: ex.Message); }

            if (IsDisposed) return;

            switch (result.Outcome)
            {
                case DebtAutoPostOutcome.AlreadyPosted:
                    SetDebtState($"✓ Posted for {result.RecordDate}", DarkTheme.PostButtonPosted, Color.FromArgb(0x4F, 0xB5, 0x6E));
                    break;
                case DebtAutoPostOutcome.Posted:
                    SetDebtState($"✓ Sent for {result.RecordDate} at {DateTime.Now:HH:mm}", DarkTheme.PostButtonPosted, Color.FromArgb(0x4F, 0xB5, 0x6E));
                    break;
                case DebtAutoPostOutcome.Failed:
                    SetDebtState("⚠ Post failed — will retry in 3 hours", Color.FromArgb(0xB8, 0x76, 0x0B), Color.FromArgb(0xB8, 0x76, 0x0B));
                    break;
                case DebtAutoPostOutcome.MissingCredentials:
                    SetDebtState("⚠ Bluesky not configured — open US Debt and click Post to configure", Color.FromArgb(0xB8, 0x76, 0x0B), Color.FromArgb(0xB8, 0x76, 0x0B));
                    break;
            }
        }
        finally { _debtPostInFlight = false; }
    }

    private void SetDebtState(string statText, Color btnColor, Color statColor)
    {
        _debtBtn.Text       = btnColor == DarkTheme.PostButtonPosted ? "✓ US Debt" : "⚠ US Debt";
        _debtBtn.BackColor  = btnColor;
        _debtStat.Text      = statText;
        _debtStat.ForeColor = statColor;
    }

    // ── Yahoo auto-post ──────────────────────────────────────────────────────────

    private async Task SafeYahooPostAsync()
    {
        if (_yahooPostInFlight) return;
        _yahooPostInFlight = true;
        try
        {
            YahooAutoPostResult result;
            try   { result = await YahooAutoPost.PostIfNeededAsync(_cts.Token); }
            catch (OperationCanceledException) { return; }
            catch (Exception ex) { result = new(YahooAutoPostOutcome.Failed, ex.Message); }

            if (IsDisposed) return;

            switch (result.Outcome)
            {
                case YahooAutoPostOutcome.RecentlyPosted:
                    SetEnergyState($"✓ Last post: {result.LastPostedAt:HH:mm}", DarkTheme.PostButtonPosted, Color.FromArgb(0x4F, 0xB5, 0x6E));
                    break;
                case YahooAutoPostOutcome.Posted:
                    SetEnergyState($"✓ Sent at {result.LastPostedAt:HH:mm}", DarkTheme.PostButtonPosted, Color.FromArgb(0x4F, 0xB5, 0x6E));
                    break;
                case YahooAutoPostOutcome.Failed:
                    SetEnergyState("⚠ Post failed — will retry in 3 hours", Color.FromArgb(0xB8, 0x76, 0x0B), Color.FromArgb(0xB8, 0x76, 0x0B));
                    break;
                case YahooAutoPostOutcome.MissingCredentials:
                    SetEnergyState("⚠ Bluesky not configured — open Energy $ and click Post to configure", Color.FromArgb(0xB8, 0x76, 0x0B), Color.FromArgb(0xB8, 0x76, 0x0B));
                    break;
            }
        }
        finally { _yahooPostInFlight = false; }
    }

    private void SetEnergyState(string statText, Color btnColor, Color statColor)
    {
        _commodityBtn.Text       = btnColor == DarkTheme.PostButtonPosted ? "✓ Energy $" : "⚠ Energy $";
        _commodityBtn.BackColor  = btnColor;
        _energyStat.Text         = statText;
        _energyStat.ForeColor    = statColor;
    }

    // ── Gas refresh ──────────────────────────────────────────────────────────────

    private void RefreshGasPriceButton()
    {
        var posted = GasPricePostTracker.IsCurrentWeekPosted();
        _gasPriceBtn.Text      = posted ? "✓ US Gas $" : "⚠ US Gas $";
        _gasPriceBtn.BackColor = posted ? DarkTheme.PostButtonPosted : Color.FromArgb(0xB8, 0x76, 0x0B);
        _gasStat.Text          = posted ? "✓ Posted this week" : "⚠ Not posted this week";
        _gasStat.ForeColor     = posted ? Color.FromArgb(0x4F, 0xB5, 0x6E) : Color.FromArgb(0xB8, 0x76, 0x0B);
    }

    // ── Layout ───────────────────────────────────────────────────────────────────

    private static Label BuildHeader()
    {
        return new Label
        {
            Text      = "GDELT Dashboard",
            Dock      = DockStyle.Top,
            Height    = 44,
            TextAlign = ContentAlignment.MiddleCenter,
            Font      = new Font("Segoe UI", 10f, FontStyle.Bold),
            ForeColor = DarkTheme.TextPrimary,
            BackColor = DarkTheme.Surface,
        };
    }

    private TableLayoutPanel BuildDashboard()
    {
        var table = new TableLayoutPanel
        {
            Dock        = DockStyle.Fill,
            ColumnCount = 2,
            RowCount    = 5,
            Padding     = new Padding(12, 8, 12, 12),
            BackColor   = DarkTheme.Background,
        };

        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 5; i++)
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));

        table.Controls.Add(MakeSearchButton("USGunV", "shooting", 1), 0, 0);
        table.Controls.Add(MakeStatLabel("Search: shooting incidents (3 hr window)"), 1, 0);

        table.Controls.Add(MakeGasPriceButton(), 0, 1);
        _gasStat = MakeStatLabel("");
        table.Controls.Add(_gasStat, 1, 1);

        table.Controls.Add(MakeQuakeButton(), 0, 2);
        table.Controls.Add(MakeStatLabel("Opens real-time USGS earthquake feed"), 1, 2);

        table.Controls.Add(MakeDebtButton(), 0, 3);
        _debtStat = MakeStatLabel("");
        table.Controls.Add(_debtStat, 1, 3);

        table.Controls.Add(MakeCommodityButton(), 0, 4);
        _energyStat = MakeStatLabel("");
        table.Controls.Add(_energyStat, 1, 4);

        return table;
    }

    private static Label MakeStatLabel(string text) => new()
    {
        Text      = text,
        AutoSize  = false,
        Dock      = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleLeft,
        ForeColor = DarkTheme.TextMuted,
        BackColor = DarkTheme.Background,
        Font      = new Font("Segoe UI", 9f),
    };

    // ── Button factories ─────────────────────────────────────────────────────────

    private Button MakeDebtButton()
    {
        _debtBtn = MakeButton("US Debt", DarkTheme.Raised);
        _debtBtn.Click += (_, _) =>
        {
            var form = new DebtForm();
            form.FormClosed += (_, _) => _ = SafeDebtPostAsync();
            form.Show();
        };
        return _debtBtn;
    }

    private Button MakeCommodityButton()
    {
        _commodityBtn = MakeButton("Energy $", DarkTheme.Raised);
        _commodityBtn.Click += (_, _) =>
        {
            var form = new CommodityForm();
            form.FormClosed += (_, _) => _ = SafeYahooPostAsync();
            form.Show();
        };
        return _commodityBtn;
    }

    private Button MakeSearchButton(string label, string query, int timespanIndex = 0)
    {
        var btn = MakeButton(label, DarkTheme.PresetBlue);
        btn.Click += (_, _) => new SearchForm(initialQuery: query, defaultTimespanIndex: timespanIndex).Show();
        return btn;
    }

    private Button MakeGasPriceButton()
    {
        _gasPriceBtn = MakeButton("US Gas $", Color.FromArgb(0x4A, 0x7C, 0x3F));
        _gasPriceBtn.Click += (_, _) =>
        {
            var form = new GasPriceForm();
            form.FormClosed += (_, _) => RefreshGasPriceButton();
            form.Show();
        };
        return _gasPriceBtn;
    }

    private Button MakeQuakeButton()
    {
        var btn = MakeButton("Quake", Color.FromArgb(0x8B, 0x45, 0x13));
        btn.Click += (_, _) => new QuakeForm().Show();
        return btn;
    }

    private static Button MakeButton(string label, Color backColor)
    {
        var btn = new Button
        {
            Text      = label,
            AutoSize  = false,
            Width     = 118,
            Height    = 30,
            Margin    = new Padding(0, 2, 8, 2),
            BackColor = backColor,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor    = Cursors.Hand,
            Font      = new Font("Segoe UI", 9f, FontStyle.Bold),
        };
        btn.FlatAppearance.BorderSize = 0;
        return btn;
    }

    // ── Disposal ─────────────────────────────────────────────────────────────────

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cts.Dispose();
            _debtTimer.Dispose();
            _yahooTimer.Dispose();
        }
        base.Dispose(disposing);
    }
}
