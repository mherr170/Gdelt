namespace GdeltSearchUI;

internal sealed class LauncherForm : Form
{
    private static readonly (string Label, string Query)[] SearchPresets =
    [
        ("USGunV", "shooting"),
    ];

    private Button _gasPriceBtn   = null!;
    private Button _debtBtn       = null!;
    private Button _commodityBtn  = null!;

    public LauncherForm()
    {
        Text = "GDELT Launcher";
        Size = new Size(360, 200);
        MinimumSize = new Size(280, 160);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9.5f);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        BackColor = DarkTheme.Background;

        Controls.Add(BuildButtonGrid());
        Controls.Add(BuildHeader());

        RefreshGasPriceButton();
        RefreshDebtButton();
        RefreshCommodityButton();
        Activated += (_, _) => { RefreshGasPriceButton(); RefreshDebtButton(); RefreshCommodityButton(); };
    }

    private void RefreshGasPriceButton()
    {
        var posted = GasPricePostTracker.IsCurrentWeekPosted();
        _gasPriceBtn.Text      = posted ? "✓ US Gas $" : "⚠ US Gas $";
        _gasPriceBtn.BackColor = posted ? Color.FromArgb(0x2E, 0x6E, 0x3E) : Color.FromArgb(0xB8, 0x76, 0x0B);
    }

    private void RefreshDebtButton()
    {
        var posted = DebtPostTracker.IsTodayPosted();
        _debtBtn.Text      = posted ? "✓ US Debt" : "⚠ US Debt";
        _debtBtn.BackColor = posted ? Color.FromArgb(0x2E, 0x6E, 0x3E) : Color.FromArgb(0xB8, 0x76, 0x0B);
    }

    private void RefreshCommodityButton()
    {
        var posted = CommodityPostTracker.IsRecentlyPosted();
        _commodityBtn.Text      = posted ? "✓ Energy $" : "⚠ Energy $";
        _commodityBtn.BackColor = posted ? Color.FromArgb(0x2E, 0x6E, 0x3E) : Color.FromArgb(0xB8, 0x76, 0x0B);
    }

    private static Label BuildHeader()
    {
        return new Label
        {
            Text = "Select a search preset",
            Dock = DockStyle.Top,
            Height = 44,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            ForeColor = DarkTheme.TextPrimary,
            BackColor = DarkTheme.Surface,
        };
    }

    private FlowLayoutPanel BuildButtonGrid()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16, 8, 16, 16),
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            BackColor = DarkTheme.Background,
        };

        foreach (var (label, query) in SearchPresets)
            panel.Controls.Add(MakeSearchButton(label, query));

        panel.Controls.Add(MakeGasPriceButton());
        panel.Controls.Add(MakeQuakeButton());
        panel.Controls.Add(MakeDebtButton());
        panel.Controls.Add(MakeCommodityButton());

        return panel;
    }

    private Button MakeDebtButton()
    {
        _debtBtn = MakeButton("US Debt", Color.FromArgb(0x6A, 0x4C, 0x93));
        _debtBtn.Click += (_, _) =>
        {
            var form = new DebtForm();
            form.FormClosed += (_, _) => RefreshDebtButton();
            form.Show();
        };
        return _debtBtn;
    }

    private Button MakeCommodityButton()
    {
        _commodityBtn = MakeButton("Energy $", Color.FromArgb(0x1A, 0x6B, 0x7A));
        _commodityBtn.Click += (_, _) =>
        {
            var form = new CommodityForm();
            form.FormClosed += (_, _) => RefreshCommodityButton();
            form.Show();
        };
        return _commodityBtn;
    }

    private Button MakeSearchButton(string label, string query)
    {
        var btn = MakeButton(label, DarkTheme.PresetBlue);
        btn.Click += (_, _) => new SearchForm(initialQuery: query).Show();
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
            Text = label,
            AutoSize = false,
            Width = 90,
            Height = 34,
            Margin = new Padding(0, 0, 8, 8),
            BackColor = backColor,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
        };
        btn.FlatAppearance.BorderSize = 0;
        return btn;
    }
}
