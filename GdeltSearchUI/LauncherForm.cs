namespace GdeltSearchUI;

internal sealed class LauncherForm : Form
{
    private Button _gasPriceBtn      = null!;
    private Button _debtBtn          = null!;
    private Button _commodityBtn     = null!;
    private Button _congressBtn      = null!;
    private Button _apodBtn          = null!;
    private Button _stockBtn         = null!;
    private Button _weatherBtn       = null!;
    private Button _blueskyMetricsBtn = null!;
    private Button _streamingBtn      = null!;
    private Button _pigsBtn           = null!;

    private Label _debtStat          = null!;
    private Label _gasStat           = null!;
    private Label _energyStat        = null!;
    private Label _congressStat      = null!;
    private Label _apodStat          = null!;
    private Label _stockStat         = null!;
    private Label _weatherStat       = null!;
    private Label _quakeStat         = null!;
    private Label _gunViolenceStat   = null!;
    private Label _blueskyMetricsStat = null!;
    private Label _streamingStat      = null!;
    private Label _pigsStat           = null!;
    private Label _birdStat           = null!;
    private Label _starterPackStat    = null!;

    private readonly System.Windows.Forms.Timer _refreshTimer;

    public LauncherForm()
    {
        Text            = "GDELT Dashboard";
        Size            = new Size(540, 654);
        MinimumSize     = new Size(400, 220);
        StartPosition   = FormStartPosition.CenterScreen;
        Font            = new Font("Segoe UI", 9.5f);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox     = false;
        BackColor       = DarkTheme.Background;

        Controls.Add(BuildDashboard());
        Controls.Add(BuildHeader());

        RefreshAllStatus();

        _refreshTimer = new System.Windows.Forms.Timer { Interval = 60_000 };
        _refreshTimer.Tick += (_, _) => RefreshAllStatus();
        _refreshTimer.Start();
    }

    private void RefreshAllStatus()
    {
        RefreshGasStatus();
        RefreshDebtStatus();
        RefreshEnergyStatus();
        RefreshCongressStatus();
        RefreshApodStatus();
        RefreshStockStatus();
        RefreshWeatherStatus();
        RefreshQuakeStatus();
        RefreshGunViolenceStatus();
        RefreshBlueskyMetricsStatus();
        RefreshStreamingStatus();
        RefreshPigsStatus();
        RefreshBirdStatus();
    }

    private void RefreshBlueskyMetricsStatus()
    {
        var hasCred = CredentialManager.Load() is not null;
        _blueskyMetricsStat.Text      = hasCred ? "✓ Account configured" : "⚠ No default account set";
        _blueskyMetricsStat.ForeColor = hasCred
            ? Color.FromArgb(0x4F, 0xB5, 0x6E)
            : Color.FromArgb(0xB8, 0x76, 0x0B);
    }

    private void RefreshStreamingStatus()
    {
        var hasCred = CredentialManager.LoadStreamingBluesky() is not null;
        _streamingStat.Text      = hasCred ? "✓ Account configured — growth only" : "⚠ No Bluesky account configured";
        _streamingStat.ForeColor = hasCred ? Color.FromArgb(0x4F, 0xB5, 0x6E) : Color.FromArgb(0xB8, 0x76, 0x0B);
    }

    private void RefreshPigsStatus()
    {
        var hasCred = CredentialManager.LoadPigsBluesky() is not null;
        _pigsStat.Text      = hasCred ? "✓ Account configured — growth only" : "⚠ No Bluesky account configured";
        _pigsStat.ForeColor = hasCred ? Color.FromArgb(0x4F, 0xB5, 0x6E) : Color.FromArgb(0xB8, 0x76, 0x0B);
    }

    private void RefreshBirdStatus()
    {
        var hasCred = CredentialManager.LoadBirdBluesky() is not null;
        var hasKey  = !string.IsNullOrWhiteSpace(CredentialManager.LoadYouTubeApiKey());
        var ready   = hasCred && hasKey;
        var last    = BirdPostTracker.GetLastPostedAt();
        var recent  = last.HasValue && (DateTime.Now - last.Value).TotalHours < 12;

        _birdStat.Text = ready
            ? (last.HasValue
                ? (recent ? $"✓ Last post: {last.Value:HH:mm}" : $"⚠ Last post: {last.Value:MMM d HH:mm}")
                : "✓ Ready — no posts yet")
            : (!hasCred ? "⚠ No Bluesky account configured"
                        : "⚠ No YouTube API key configured");
        _birdStat.ForeColor = (ready && (!last.HasValue || recent))
            ? Color.FromArgb(0x4F, 0xB5, 0x6E)
            : Color.FromArgb(0xB8, 0x76, 0x0B);
    }

    // ── Status refresh (reads local tracker files — no API calls) ─────────────

    private void RefreshGasStatus()
    {
        var posted = GasPricePostTracker.IsCurrentWeekPosted();
        _gasPriceBtn.Text      = posted ? "✓ US Gas $"  : "⚠ US Gas $";
        _gasPriceBtn.BackColor = posted ? DarkTheme.PostButtonPosted : Color.FromArgb(0xB8, 0x76, 0x0B);
        _gasStat.Text          = posted ? "✓ Posted this week"       : "⚠ Not posted this week";
        _gasStat.ForeColor     = posted ? Color.FromArgb(0x4F, 0xB5, 0x6E) : Color.FromArgb(0xB8, 0x76, 0x0B);
    }

    private void RefreshDebtStatus()
    {
        var posted = DebtPostTracker.IsTodayPosted();
        _debtBtn.Text      = posted ? "✓ US Debt"  : "⚠ US Debt";
        _debtBtn.BackColor = posted ? DarkTheme.PostButtonPosted : Color.FromArgb(0xB8, 0x76, 0x0B);
        _debtStat.Text     = posted ? "✓ Posted recently"      : "⚠ Not posted recently";
        _debtStat.ForeColor = posted ? Color.FromArgb(0x4F, 0xB5, 0x6E) : Color.FromArgb(0xB8, 0x76, 0x0B);
    }

    private void RefreshEnergyStatus()
    {
        var lastPost = YahooPostTracker.GetLastPostedAt();
        var recent   = lastPost.HasValue && (DateTime.Now - lastPost.Value).TotalHours < 8;
        _commodityBtn.Text      = recent ? "✓ Energy $"  : "⚠ Energy $";
        _commodityBtn.BackColor = recent ? DarkTheme.PostButtonPosted : Color.FromArgb(0xB8, 0x76, 0x0B);
        _energyStat.Text        = recent ? $"✓ Last post: {lastPost!.Value:HH:mm}" : "⚠ Not posted recently";
        _energyStat.ForeColor   = recent ? Color.FromArgb(0x4F, 0xB5, 0x6E) : Color.FromArgb(0xB8, 0x76, 0x0B);
    }

    private void RefreshStockStatus()
    {
        var eastern  = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        var etToday  = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, eastern).Date.ToString("yyyy-MM-dd");
        var posted   = StockPostTracker.HasBeenPosted(etToday);
        _stockBtn.BackColor  = posted ? DarkTheme.PostButtonPosted : Color.FromArgb(0x1A, 0x6A, 0x3A);
        _stockStat.Text      = posted ? "✓ Posted today" : "⚠ Not posted today";
        _stockStat.ForeColor = posted ? Color.FromArgb(0x4F, 0xB5, 0x6E) : Color.FromArgb(0xB8, 0x76, 0x0B);
    }

    private void RefreshWeatherStatus()
    {
        var hasCred = CredentialManager.LoadWeatherBluesky() is not null;
        _weatherBtn.BackColor  = hasCred ? DarkTheme.Raised : Color.FromArgb(0xB8, 0x76, 0x0B);
        _weatherStat.Text      = hasCred ? "✓ Account configured — monitors every 10 min" : "⚠ No Bluesky account configured";
        _weatherStat.ForeColor = hasCred ? Color.FromArgb(0x4F, 0xB5, 0x6E) : Color.FromArgb(0xB8, 0x76, 0x0B);
    }

    private void RefreshApodStatus()
    {
        var posted = ApodPostTracker.HasBeenPosted(DateTime.Today.ToString("yyyy-MM-dd"));
        _apodBtn.BackColor  = posted ? DarkTheme.PostButtonPosted : Color.FromArgb(0x1A, 0x56, 0x7A);
        _apodStat.Text      = posted ? "✓ Posted today" : "⚠ Not posted today";
        _apodStat.ForeColor = posted ? Color.FromArgb(0x4F, 0xB5, 0x6E) : Color.FromArgb(0xB8, 0x76, 0x0B);
    }

    private void RefreshQuakeStatus()
    {
        var last   = QuakePostTracker.GetLastPostedAt();
        var recent = last.HasValue && (DateTime.Now - last.Value).TotalHours < 24;
        _quakeStat.Text      = last.HasValue
            ? (recent ? $"✓ Last post: {last.Value:HH:mm}" : $"⚠ Last post: {last.Value:MMM d}")
            : "⚠ No posts recorded";
        _quakeStat.ForeColor = recent ? Color.FromArgb(0x4F, 0xB5, 0x6E) : Color.FromArgb(0xB8, 0x76, 0x0B);
    }

    private void RefreshGunViolenceStatus()
    {
        var last   = GunViolencePostTracker.GetLastPostedAt();
        var recent = last.HasValue && (DateTime.Now - last.Value).TotalHours < 12;
        _gunViolenceStat.Text      = last.HasValue
            ? (recent ? $"✓ Last post: {last.Value:HH:mm}" : $"⚠ Last post: {last.Value:MMM d HH:mm}")
            : "⚠ No posts recorded";
        _gunViolenceStat.ForeColor = recent ? Color.FromArgb(0x4F, 0xB5, 0x6E) : Color.FromArgb(0xB8, 0x76, 0x0B);
    }

    private void RefreshCongressStatus()
    {
        var hasKey  = !string.IsNullOrWhiteSpace(CredentialManager.LoadProPublicaKey());
        var hasCred = CredentialManager.LoadCongressBluesky() is not null;
        var ready   = hasKey && hasCred;
        _congressBtn.BackColor = ready ? DarkTheme.Raised : Color.FromArgb(0xB8, 0x76, 0x0B);
        _congressStat.Text     = ready   ? "✓ API key + account configured"
                               : !hasKey ? "⚠ No ProPublica API key"
                               :           "⚠ No Bluesky account configured";
        _congressStat.ForeColor = ready ? Color.FromArgb(0x4F, 0xB5, 0x6E) : Color.FromArgb(0xB8, 0x76, 0x0B);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _refreshTimer.Stop();
        _refreshTimer.Dispose();
        base.OnFormClosed(e);
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
            RowCount    = 14,
            Padding     = new Padding(12, 8, 12, 12),
            BackColor   = DarkTheme.Background,
        };

        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 14; i++)
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));

        table.Controls.Add(MakeGunViolenceButton(), 0, 0);
        _gunViolenceStat = MakeStatLabel("");
        table.Controls.Add(MakeGunViolenceStatCell(), 1, 0);

        table.Controls.Add(MakeGasPriceButton(), 0, 1);
        _gasStat = MakeStatLabel("");
        table.Controls.Add(_gasStat, 1, 1);

        table.Controls.Add(MakeQuakeButton(), 0, 2);
        _quakeStat = MakeStatLabel("");
        table.Controls.Add(_quakeStat, 1, 2);

        table.Controls.Add(MakeDebtButton(), 0, 3);
        _debtStat = MakeStatLabel("");
        table.Controls.Add(_debtStat, 1, 3);

        table.Controls.Add(MakeCommodityButton(), 0, 4);
        _energyStat = MakeStatLabel("");
        table.Controls.Add(_energyStat, 1, 4);

        table.Controls.Add(MakeCongressButton(), 0, 5);
        _congressStat = MakeStatLabel("");
        table.Controls.Add(_congressStat, 1, 5);

        table.Controls.Add(MakeApodButton(), 0, 6);
        _apodStat = MakeStatLabel("");
        table.Controls.Add(_apodStat, 1, 6);

        table.Controls.Add(MakeStockButton(), 0, 7);
        _stockStat = MakeStatLabel("");
        table.Controls.Add(_stockStat, 1, 7);

        table.Controls.Add(MakeWeatherButton(), 0, 8);
        _weatherStat = MakeStatLabel("");
        table.Controls.Add(_weatherStat, 1, 8);

        table.Controls.Add(MakeBlueskyMetricsButton(), 0, 9);
        _blueskyMetricsStat = MakeStatLabel("");
        table.Controls.Add(_blueskyMetricsStat, 1, 9);

        table.Controls.Add(MakeStreamingButton(), 0, 10);
        _streamingStat = MakeStatLabel("");
        table.Controls.Add(_streamingStat, 1, 10);

        table.Controls.Add(MakePigsButton(), 0, 11);
        _pigsStat = MakeStatLabel("");
        table.Controls.Add(_pigsStat, 1, 11);

        table.Controls.Add(MakeBirdButton(), 0, 12);
        _birdStat = MakeStatLabel("");
        table.Controls.Add(_birdStat, 1, 12);

        table.Controls.Add(MakeStarterPackButton(), 0, 13);
        _starterPackStat = MakeStatLabel("");
        table.Controls.Add(_starterPackStat, 1, 13);

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
            form.FormClosed += (_, _) => RefreshDebtStatus();
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
            form.FormClosed += (_, _) => RefreshEnergyStatus();
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

    private Button MakeGunViolenceButton()
    {
        var btn = MakeButton("USGunV", DarkTheme.PresetBlue);
        btn.Click += (_, _) => new SearchForm(
            initialQuery: "shooting",
            defaultTimespanIndex: 1,
            credLoader: CredentialManager.LoadGunViolenceBluesky,
            credSaver:  CredentialManager.SaveGunViolenceBluesky,
            credTitle:  "Bluesky Account — US Gun Violence").Show();
        return btn;
    }

    // Stat label plus a small key button for the GNews fallback source (used
    // when GDELT rate-limits) — gun violence has no dedicated settings form to
    // hang an "API Key" button off of like APOD/gas prices do, so it lives here.
    private Panel MakeGunViolenceStatCell()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = DarkTheme.Background };

        var keyBtn = new Button
        {
            Text      = "🔑",
            Dock      = DockStyle.Right,
            Width     = 26,
            FlatStyle = FlatStyle.Flat,
            BackColor = DarkTheme.Raised,
            ForeColor = DarkTheme.TextPrimary,
        };
        keyBtn.FlatAppearance.BorderColor = DarkTheme.Input;

        var current = CredentialManager.LoadGNewsApiKey() ?? "";
        var toolTip = new ToolTip();
        toolTip.SetToolTip(keyBtn,
            $"GNews API key (fallback when GDELT rate-limits) — {(current.Length > 0 ? "set" : "not set")}");

        keyBtn.Click += (_, _) =>
        {
            var key = ApiKeyPrompt.Show(this, "GNews API key (free at gnews.io — used as a fallback when GDELT rate-limits):");
            if (key is not null)
            {
                CredentialManager.SaveGNewsApiKey(key);
                toolTip.SetToolTip(keyBtn, "GNews API key (fallback when GDELT rate-limits) — set");
            }
        };

        panel.Controls.Add(_gunViolenceStat);
        panel.Controls.Add(keyBtn);
        return panel;
    }

    // Manually re-runs StarterPackSync across the roster — this is an occasional
    // curation action (run once, re-run only if the roster changes), not a
    // scheduled worker, so it lives here rather than in the auto-post service.
    private Button MakeStarterPackButton()
    {
        var btn = MakeButton("🔗 Live Wire", DarkTheme.Raised);
        btn.Click += async (_, _) =>
        {
            btn.Enabled = false;
            _starterPackStat.Text      = "Syncing…";
            _starterPackStat.ForeColor = DarkTheme.TextMuted;

            var lines = new List<string>();
            try
            {
                await StarterPackSync.SyncAllAsync(line =>
                {
                    lines.Add(line);
                    PostLogger.Info("starterpack", line);
                });

                var failures = lines.Count(l => l.Contains("Failed") || l.Contains("skipped"));
                _starterPackStat.Text      = failures == 0 ? "✓ Synced all accounts" : $"⚠ {failures} issue(s) — see log";
                _starterPackStat.ForeColor = failures == 0
                    ? Color.FromArgb(0x4F, 0xB5, 0x6E)
                    : Color.FromArgb(0xB8, 0x76, 0x0B);
            }
            catch (Exception ex)
            {
                PostLogger.Error("starterpack", $"Sync aborted: {ex.Message}");
                _starterPackStat.Text      = "✗ Sync failed — see log";
                _starterPackStat.ForeColor = Color.FromArgb(0xC0, 0x50, 0x50);
            }
            finally
            {
                btn.Enabled = true;
            }

            MessageBox.Show(string.Join(Environment.NewLine, lines), "Starter Pack Sync",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        };
        return btn;
    }

    private Button MakeGasPriceButton()
    {
        _gasPriceBtn = MakeButton("US Gas $", Color.FromArgb(0x4A, 0x7C, 0x3F));
        _gasPriceBtn.Click += (_, _) =>
        {
            var form = new GasPriceForm();
            form.FormClosed += (_, _) => RefreshGasStatus();
            form.Show();
        };
        return _gasPriceBtn;
    }

    private Button MakeQuakeButton()
    {
        var btn = MakeButton("Quake", Color.FromArgb(0x8B, 0x45, 0x13));
        btn.Click += (_, _) =>
        {
            var form = new QuakeForm();
            form.FormClosed += (_, _) => RefreshQuakeStatus();
            form.Show();
        };
        return btn;
    }

    private Button MakeCongressButton()
    {
        _congressBtn = MakeButton("Congress", Color.FromArgb(0x3A, 0x5A, 0x8A));
        _congressBtn.Click += (_, _) =>
        {
            var form = new CongressForm();
            form.FormClosed += (_, _) => RefreshCongressStatus();
            form.Show();
        };
        return _congressBtn;
    }

    private Button MakeStockButton()
    {
        _stockBtn = MakeButton("Stocks", Color.FromArgb(0x1A, 0x6A, 0x3A));
        _stockBtn.Click += (_, _) =>
        {
            var form = new StockForm();
            form.FormClosed += (_, _) => RefreshStockStatus();
            form.Show();
        };
        return _stockBtn;
    }

    private Button MakeWeatherButton()
    {
        _weatherBtn = MakeButton("⚡ Weather", Color.FromArgb(0x7A, 0x3A, 0x10));
        _weatherBtn.Click += (_, _) =>
        {
            var form = new WeatherForm();
            form.FormClosed += (_, _) => RefreshWeatherStatus();
            form.Show();
        };
        return _weatherBtn;
    }

    private Button MakeApodButton()
    {
        _apodBtn = MakeButton("NASA APOD", Color.FromArgb(0x1A, 0x56, 0x7A));
        _apodBtn.Click += (_, _) =>
        {
            var form = new ApodForm();
            form.FormClosed += (_, _) => RefreshApodStatus();
            form.Show();
        };
        return _apodBtn;
    }

    private Button MakeBlueskyMetricsButton()
    {
        _blueskyMetricsBtn = MakeButton("☁ Bsky Stats", Color.FromArgb(0x00, 0x6A, 0xB0));
        _blueskyMetricsBtn.Click += (_, _) =>
        {
            var form = new BlueskyMetricsHub();
            form.FormClosed += (_, _) => RefreshBlueskyMetricsStatus();
            form.Show();
        };
        return _blueskyMetricsBtn;
    }

    private Button MakeStreamingButton()
    {
        _streamingBtn = MakeButton("Streaming", Color.FromArgb(0x55, 0x2B, 0x8A));
        _streamingBtn.Click += (_, _) =>
        {
            using var dlg = new SettingsDialog(
                CredentialManager.LoadStreamingBluesky,
                CredentialManager.SaveStreamingBluesky,
                "Bluesky Account — Streaming");
            if (dlg.ShowDialog() == DialogResult.OK)
                RefreshStreamingStatus();
        };
        return _streamingBtn;
    }

    private Button MakePigsButton()
    {
        _pigsBtn = MakeButton("Pigs Gonna Blow", Color.FromArgb(0x8A, 0x3A, 0x5A));
        _pigsBtn.Click += (_, _) =>
        {
            using var dlg = new SettingsDialog(
                CredentialManager.LoadPigsBluesky,
                CredentialManager.SavePigsBluesky,
                "Bluesky Account — Pigs Gonna Blow");
            if (dlg.ShowDialog() == DialogResult.OK)
                RefreshPigsStatus();
        };
        return _pigsBtn;
    }

    private Button _birdBtn = null!;

    private Button MakeBirdButton()
    {
        _birdBtn = MakeButton("NJ Birds", Color.FromArgb(0x2E, 0x6B, 0x3E));
        _birdBtn.Click += (_, _) =>
        {
            using var dlg = new BirdSettingsDialog();
            if (dlg.ShowDialog() == DialogResult.OK)
                RefreshBirdStatus();
        };
        return _birdBtn;
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
}
