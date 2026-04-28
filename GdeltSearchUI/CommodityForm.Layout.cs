namespace GdeltSearchUI;

internal partial class CommodityForm
{
    // ── Top toolbar ───────────────────────────────────────────────────────────

    private Panel BuildToolbar()
    {
        var bar = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 48,
            BackColor = DarkTheme.Surface,
            Padding   = new Padding(10, 8, 10, 8),
        };

        var accountBtn = new Button
        {
            Text      = "⚙ Account",
            Dock      = DockStyle.Right,
            Width     = 84,
            BackColor = DarkTheme.Raised,
            ForeColor = DarkTheme.TextPrimary,
            FlatStyle = FlatStyle.Flat,
            Cursor    = Cursors.Hand,
        };
        accountBtn.FlatAppearance.BorderSize = 0;
        accountBtn.Click += (_, _) =>
        {
            using var dlg = new SettingsDialog(
                CredentialManager.LoadCommodityBluesky,
                CredentialManager.SaveCommodityBluesky,
                "Bluesky Account — Commodities");
            dlg.ShowDialog(this);
        };

        _postButton = new Button
        {
            Text      = "Post",
            Dock      = DockStyle.Right,
            Width     = 140,
            BackColor = DarkTheme.PostButtonDefault,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor    = Cursors.Hand,
            Enabled   = false,
        };
        _postButton.FlatAppearance.BorderSize = 0;
        _postButton.Click += async (_, _) => await PostToBlueskyAsync();

        _refreshButton = new Button
        {
            Text      = "Refresh",
            Dock      = DockStyle.Right,
            Width     = 70,
            BackColor = DarkTheme.AccentBlue,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor    = Cursors.Hand,
        };
        _refreshButton.FlatAppearance.BorderSize = 0;
        _refreshButton.Click += async (_, _) => await FetchAsync();

        var heading = new Label
        {
            Text      = "Energy Spot Prices",
            Dock      = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = DarkTheme.TextMuted,
            BackColor = Color.Transparent,
            Font      = new Font("Segoe UI", 8.5f),
        };

        // Right-docked controls added in reverse visual order (rightmost first)
        bar.Controls.Add(accountBtn);
        bar.Controls.Add(_postButton);
        bar.Controls.Add(_refreshButton);
        bar.Controls.Add(heading);
        return bar;
    }

    // ── Two-card data area ────────────────────────────────────────────────────

    private Control BuildDataArea()
    {
        // Outer container with breathing room around and between cards
        var outer = new TableLayoutPanel
        {
            Dock        = DockStyle.Fill,
            RowCount    = 3,
            ColumnCount = 1,
            Padding     = new Padding(8, 6, 8, 6),
            BackColor   = DarkTheme.Background,
        };
        outer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        outer.RowStyles.Add(new RowStyle(SizeType.Percent, 50));   // EIA card
        outer.RowStyles.Add(new RowStyle(SizeType.Absolute, 6));   // gap
        outer.RowStyles.Add(new RowStyle(SizeType.Percent, 50));   // Yahoo card

        outer.Controls.Add(BuildEiaCard(),   0, 0);
        outer.Controls.Add(BuildYahooCard(), 0, 2);
        return outer;
    }

    private Panel BuildEiaCard()
    {
        var (border, content) = MakeCard();

        var hdr   = MakeCardHeader("EIA ENERGY SPOT PRICES");
        _eiaStatusLabel = MakeCardStatus("Loading…");
        var table = BuildEiaTable();

        // DockStyle order matters: Bottom before Fill before Top
        content.Controls.Add(_eiaStatusLabel);
        content.Controls.Add(table);
        content.Controls.Add(hdr);
        return border;
    }

    private Panel BuildYahooCard()
    {
        var (border, content) = MakeCard();

        var hdr   = MakeCardHeader("YAHOO FINANCE FUTURES  (~15 min delayed)");
        _yahooStatusLabel = MakeCardStatus("Loading…");
        var table = BuildYahooTable();

        content.Controls.Add(_yahooStatusLabel);
        content.Controls.Add(table);
        content.Controls.Add(hdr);
        return border;
    }

    // ── Card frame helpers ────────────────────────────────────────────────────

    private static (Panel border, Panel content) MakeCard()
    {
        var content = new Panel
        {
            Dock      = DockStyle.Fill,
            BackColor = DarkTheme.Surface,
        };
        // 1-px Raised border achieved by padding a slightly lighter backing panel
        var border = new Panel
        {
            Dock      = DockStyle.Fill,
            BackColor = DarkTheme.Raised,
            Padding   = new Padding(1),
        };
        border.Controls.Add(content);
        return (border, content);
    }

    private static Label MakeCardHeader(string text) => new()
    {
        Text      = text,
        Dock      = DockStyle.Top,
        Height    = 26,
        TextAlign = ContentAlignment.MiddleLeft,
        ForeColor = DarkTheme.TextPrimary,
        BackColor = Color.FromArgb(0x2A, 0x2A, 0x2E),
        Font      = new Font("Segoe UI", 8f, FontStyle.Bold),
        Padding   = new Padding(8, 0, 0, 0),
    };

    private static Label MakeCardStatus(string text) => new()
    {
        Text      = text,
        Dock      = DockStyle.Bottom,
        Height    = 21,
        TextAlign = ContentAlignment.MiddleLeft,
        ForeColor = DarkTheme.TextMuted,
        BackColor = Color.FromArgb(0x22, 0x22, 0x25),
        Font      = new Font("Segoe UI", 8f),
        Padding   = new Padding(8, 0, 0, 0),
    };

    // ── EIA data table ────────────────────────────────────────────────────────

    private Panel BuildEiaTable()
    {
        var catalog = CommodityApiClient.Catalog;
        var n       = catalog.Length;
        _priceLabels = new Label[n];
        _deltaLabels = new Label[n];

        var table = MakeDataTable(n);
        for (var i = 0; i < n; i++)
        {
            var (_, displayName, unit, _, _) = catalog[i];
            var (name, price, delta) = AddDataRow(table, i, $"{displayName}  ({unit})");
            _priceLabels[i] = price;
            _deltaLabels[i] = delta;
        }
        return table;
    }

    // ── Yahoo Finance data table ──────────────────────────────────────────────

    private Panel BuildYahooTable()
    {
        var catalog = YahooFinanceApiClient.Catalog;
        var n       = catalog.Length;
        _oilPriceLabels      = new Label[n];
        _oilPriceDeltaLabels = new Label[n];

        var table = MakeDataTable(n);
        for (var j = 0; j < n; j++)
        {
            var (_, _, displayName, unit) = catalog[j];
            var (name, price, delta) = AddDataRow(table, j, $"{displayName}  ({unit})");
            _oilPriceLabels[j]      = price;
            _oilPriceDeltaLabels[j] = delta;
        }
        return table;
    }

    // ── Shared table/row factories ────────────────────────────────────────────

    private static TableLayoutPanel MakeDataTable(int rowCount)
    {
        var t = new TableLayoutPanel
        {
            Dock        = DockStyle.Fill,
            ColumnCount = 3,
            RowCount    = rowCount,
            Padding     = new Padding(10, 4, 10, 2),
            BackColor   = DarkTheme.Surface,
        };
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));
        t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28));
        for (var i = 0; i < rowCount; i++)
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        return t;
    }

    private static (Label name, Label price, Label delta) AddDataRow(
        TableLayoutPanel table, int row, string labelText)
    {
        var name = new Label
        {
            Text      = labelText,
            Dock      = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = DarkTheme.TextMuted,
            Font      = new Font("Segoe UI", 9.5f),
        };
        var price = new Label
        {
            Text      = "—",
            Dock      = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            ForeColor = DarkTheme.TextPrimary,
            Font      = new Font("Segoe UI", 13f, FontStyle.Bold),
        };
        var delta = new Label
        {
            Text      = "",
            Dock      = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            ForeColor = DarkTheme.TextMuted,
            Font      = new Font("Segoe UI", 9f),
            Padding   = new Padding(4, 0, 0, 0),
        };
        table.Controls.Add(name,  0, row);
        table.Controls.Add(price, 1, row);
        table.Controls.Add(delta, 2, row);
        return (name, price, delta);
    }

    // ── Global status bar (Bluesky / operational messages only) ──────────────

    private Label BuildStatusLabel() => _statusLabel = CreateStatusLabel();
}
