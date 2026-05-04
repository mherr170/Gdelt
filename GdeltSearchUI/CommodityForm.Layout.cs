namespace GdeltSearchUI;

internal partial class CommodityForm
{
    private Control BuildDataArea()
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
                CredentialManager.LoadYahooBluesky,
                CredentialManager.SaveYahooBluesky,
                "Bluesky Account — Yahoo Finance Futures");
            dlg.ShowDialog(this);
        };

        _yahooPostButton = new Button
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
        _yahooPostButton.FlatAppearance.BorderSize = 0;
        _yahooPostButton.Click += async (_, _) => await PostYahooToBlueskyAsync();

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

        hdr.Controls.Add(accountBtn);
        hdr.Controls.Add(_yahooPostButton);
        hdr.Controls.Add(_yahooRefreshButton);
        hdr.Controls.Add(title);
        return hdr;
    }

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

    private static (Panel border, Panel content) MakeCard()
    {
        var content = new Panel { Dock = DockStyle.Fill, BackColor = DarkTheme.Surface };
        var border  = new Panel
        {
            Dock      = DockStyle.Fill,
            BackColor = DarkTheme.Raised,
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

    private Label BuildStatusLabel() => _statusLabel = CreateStatusLabel();
}
