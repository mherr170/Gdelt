namespace GdeltSearchUI;

internal partial class CommodityForm
{
    private static readonly (int Start, int End, string Header)[] Sections =
    [
        (0, 4, "Energy"),
    ];

    private Panel BuildToolbar()
    {
        var bar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 48,
            BackColor = DarkTheme.Surface,
            Padding = new Padding(10, 8, 10, 8),
        };

        var accountBtn = new Button
        {
            Text = "⚙ Account",
            Dock = DockStyle.Right,
            Width = 84,
            BackColor = DarkTheme.Raised,
            ForeColor = DarkTheme.TextPrimary,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
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

        var oilKeyBtn = new Button
        {
            Text      = "⚙ OilPrice",
            Dock      = DockStyle.Right,
            Width     = 80,
            BackColor = DarkTheme.Raised,
            ForeColor = DarkTheme.TextPrimary,
            FlatStyle = FlatStyle.Flat,
            Cursor    = Cursors.Hand,
        };
        oilKeyBtn.FlatAppearance.BorderSize = 0;
        oilKeyBtn.Click += (_, _) =>
        {
            var current = CredentialManager.LoadOilPriceApiKey() ?? "";
            var key = PromptForApiKey(
                $"OilPriceAPI.com key (oilpriceapi.com){(current.Length > 0 ? " — leave blank to keep existing" : "")}:");
            if (key is not null)
                CredentialManager.SaveOilPriceApiKey(key);
        };

        _postButton = new Button
        {
            Text = "Post",
            Dock = DockStyle.Right,
            Width = 140,
            BackColor = DarkTheme.PostButtonDefault,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Enabled = false,
        };
        _postButton.FlatAppearance.BorderSize = 0;
        _postButton.Click += async (_, _) => await PostToBlueskyAsync();

        _refreshButton = new Button
        {
            Text = "Refresh",
            Dock = DockStyle.Right,
            Width = 70,
            BackColor = DarkTheme.AccentBlue,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
        };
        _refreshButton.FlatAppearance.BorderSize = 0;
        _refreshButton.Click += async (_, _) => await FetchAsync();

        var heading = new Label
        {
            Text = "EIA Daily Energy Spot Prices",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = DarkTheme.TextMuted,
            BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 8.5f),
        };

        // Right-docked controls added in reverse visual order (rightmost first)
        bar.Controls.Add(accountBtn);
        bar.Controls.Add(oilKeyBtn);
        bar.Controls.Add(_postButton);
        bar.Controls.Add(_refreshButton);
        bar.Controls.Add(heading);
        return bar;
    }

    private Panel BuildPricePanel()
    {
        var catalog    = CommodityApiClient.Catalog;
        var n          = catalog.Length;
        var oilCatalog = OilPriceApiClient.Catalog;
        var nOil       = oilCatalog.Length;
        // rows = EIA section header + EIA data + OilPrice section header + OilPrice data
        var nRows = n + Sections.Length + nOil + 1;

        _priceLabels          = new Label[n];
        _deltaLabels          = new Label[n];
        _oilPriceLabels       = new Label[nOil];
        _oilPriceDeltaLabels  = new Label[nOil];

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = nRows,
            Padding = new Padding(16, 6, 16, 6),
            BackColor = DarkTheme.Background,
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28));

        var tableRow = 0;
        var catIdx   = 0;

        foreach (var (start, end, header) in Sections)
        {
            var hdr = new Label
            {
                Text = header.ToUpperInvariant(),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = DarkTheme.TextMuted,
                BackColor = DarkTheme.Surface,
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                Padding = new Padding(4, 0, 0, 0),
            };
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            table.Controls.Add(hdr, 0, tableRow);
            table.SetColumnSpan(hdr, 3);
            tableRow++;

            for (var i = start; i <= end; i++)
            {
                var (_, displayName, unit, _, _) = catalog[catIdx];
                var ci = catIdx;

                var nameLabel = new Label
                {
                    Text = $"{displayName}  ({unit})",
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft,
                    ForeColor = DarkTheme.TextMuted,
                    Font = new Font("Segoe UI", 9.5f),
                };

                var priceLabel = new Label
                {
                    Text = "—",
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleRight,
                    ForeColor = DarkTheme.TextPrimary,
                    Font = new Font("Segoe UI", 13f, FontStyle.Bold),
                };

                var deltaLabel = new Label
                {
                    Text = "",
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleRight,
                    ForeColor = DarkTheme.TextMuted,
                    Font = new Font("Segoe UI", 9f),
                    Padding = new Padding(4, 0, 0, 0),
                };

                _priceLabels[ci] = priceLabel;
                _deltaLabels[ci] = deltaLabel;

                table.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
                table.Controls.Add(nameLabel,  0, tableRow);
                table.Controls.Add(priceLabel, 1, tableRow);
                table.Controls.Add(deltaLabel, 2, tableRow);
                tableRow++;
                catIdx++;
            }
        }

        // ── OilPriceAPI.com section ───────────────────────────────────────────
        var oilHdr = new Label
        {
            Text      = "OILPRICE API (live)",
            Dock      = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = DarkTheme.TextMuted,
            BackColor = DarkTheme.Surface,
            Font      = new Font("Segoe UI", 8f, FontStyle.Bold),
            Padding   = new Padding(4, 0, 0, 0),
        };
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        table.Controls.Add(oilHdr, 0, tableRow);
        table.SetColumnSpan(oilHdr, 3);
        tableRow++;

        for (var j = 0; j < oilCatalog.Length; j++)
        {
            var (_, displayName, unit) = oilCatalog[j];
            var jj = j;

            var nameLabel = new Label
            {
                Text      = $"{displayName}  ({unit})",
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = DarkTheme.TextMuted,
                Font      = new Font("Segoe UI", 9.5f),
            };

            var priceLabel = new Label
            {
                Text      = "—",
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = DarkTheme.TextPrimary,
                Font      = new Font("Segoe UI", 13f, FontStyle.Bold),
            };

            var deltaLabel = new Label
            {
                Text      = "",
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = DarkTheme.TextMuted,
                Font      = new Font("Segoe UI", 9f),
                Padding   = new Padding(4, 0, 0, 0),
            };

            _oilPriceLabels[jj]      = priceLabel;
            _oilPriceDeltaLabels[jj] = deltaLabel;

            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
            table.Controls.Add(nameLabel,  0, tableRow);
            table.Controls.Add(priceLabel, 1, tableRow);
            table.Controls.Add(deltaLabel, 2, tableRow);
            tableRow++;
        }

        return table;
    }

    private Label BuildStatusLabel() => _statusLabel = CreateStatusLabel();
}
