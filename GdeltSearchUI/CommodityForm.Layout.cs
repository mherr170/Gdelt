namespace GdeltSearchUI;

internal partial class CommodityForm
{
    // ── Two-card data area ────────────────────────────────────────────────────

    private Control BuildDataArea()
    {
        var outer = new TableLayoutPanel
        {
            Dock        = DockStyle.Fill,
            RowCount    = 3,
            ColumnCount = 1,
            Padding     = new Padding(8, 8, 8, 6),
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

    // ── EIA card ──────────────────────────────────────────────────────────────

    private Panel BuildEiaCard()
    {
        var (border, content) = MakeCard();

        _eiaStatusLabel = MakeCardStatus("Loading…");

        var hdr   = BuildEiaHeader();
        var table = BuildEiaTable();

        content.Controls.Add(_eiaStatusLabel);   // Bottom
        content.Controls.Add(table);              // Fill
        content.Controls.Add(hdr);               // Top
        return border;
    }

    private Panel BuildEiaHeader()
    {
        var hdr = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 40,
            BackColor = Color.FromArgb(0x2A, 0x2A, 0x2E),
            Padding   = new Padding(8, 0, 4, 0),
        };

        // ⚙ Bluesky account
        var accountBtn = new Button
        {
            Text      = "⚙",
            Dock      = DockStyle.Right,
            Width     = 30,
            BackColor = DarkTheme.Raised,
            ForeColor = DarkTheme.TextPrimary,
            FlatStyle = FlatStyle.Flat,
            Cursor    = Cursors.Hand,
            Font      = new Font("Segoe UI", 9f),
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

        // Post to Bluesky
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
            Font      = new Font("Segoe UI", 8.5f),
        };
        _postButton.FlatAppearance.BorderSize = 0;
        _postButton.Click += async (_, _) => await PostToBlueskyAsync();

        // Refresh EIA only
        _eiaRefreshButton = new Button
        {
            Text      = "Refresh",
            Dock      = DockStyle.Right,
            Width     = 68,
            BackColor = DarkTheme.AccentBlue,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor    = Cursors.Hand,
            Font      = new Font("Segoe UI", 8.5f),
        };
        _eiaRefreshButton.FlatAppearance.BorderSize = 0;
        _eiaRefreshButton.Click += async (_, _) => await FetchEiaAsync();

        var title = new Label
        {
            Text      = "EIA ENERGY SPOT PRICES",
            Dock      = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = DarkTheme.TextPrimary,
            BackColor = Color.Transparent,
            Font      = new Font("Segoe UI", 8f, FontStyle.Bold),
        };

        // Right-docked in reverse visual order (rightmost first)
        hdr.Controls.Add(accountBtn);
        hdr.Controls.Add(_postButton);
        hdr.Controls.Add(_eiaRefreshButton);
        hdr.Controls.Add(title);
        return hdr;
    }

    // ── Yahoo Finance card ────────────────────────────────────────────────────

    private Panel BuildYahooCard()
    {
        var (border, content) = MakeCard();

        _yahooStatusLabel = MakeCardStatus("Loading…");

        var hdr   = BuildYahooHeader();
        var table = BuildYahooTable();

        content.Controls.Add(_yahooStatusLabel);
        content.Controls.Add(table);
        content.Controls.Add(hdr);
        return border;
    }

    private Panel BuildYahooHeader()
    {
        var hdr = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 40,
            BackColor = Color.FromArgb(0x2A, 0x2A, 0x2E),
            Padding   = new Padding(8, 0, 4, 0),
        };

        // Refresh Yahoo only
        _yahooRefreshButton = new Button
        {
            Text      = "Refresh",
            Dock      = DockStyle.Right,
            Width     = 68,
            BackColor = DarkTheme.AccentBlue,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor    = Cursors.Hand,
            Font      = new Font("Segoe UI", 8.5f),
        };
        _yahooRefreshButton.FlatAppearance.BorderSize = 0;
        _yahooRefreshButton.Click += async (_, _) => await FetchYahooAsync();

        var title = new Label
        {
            Text      = "YAHOO FINANCE FUTURES  (~15 min delayed)",
            Dock      = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = DarkTheme.TextPrimary,
            BackColor = Color.Transparent,
            Font      = new Font("Segoe UI", 8f, FontStyle.Bold),
        };

        hdr.Controls.Add(_yahooRefreshButton);
        hdr.Controls.Add(title);
        return hdr;
    }

    // ── Card frame helpers ────────────────────────────────────────────────────

    private static (Panel border, Panel content) MakeCard()
    {
        var content = new Panel { Dock = DockStyle.Fill, BackColor = DarkTheme.Surface };
        var border  = new Panel
        {
            Dock      = DockStyle.Fill,
            BackColor = DarkTheme.Raised,   // 1-px border via padding
            Padding   = new Padding(1),
        };
        border.Controls.Add(content);
        return (border, content);
    }

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
            var (_, price, delta) = AddDataRow(table, i, $"{displayName}  ({unit})");
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
            var (_, price, delta) = AddDataRow(table, j, $"{displayName}  ({unit})");
            _oilPriceLabels[j]      = price;
            _oilPriceDeltaLabels[j] = delta;
        }
        return table;
    }

    // ── Shared table / row factories ──────────────────────────────────────────

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
            t.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
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

    // ── Global status bar (Bluesky / posting feedback only) ──────────────────

    private Label BuildStatusLabel() => _statusLabel = CreateStatusLabel();
}
