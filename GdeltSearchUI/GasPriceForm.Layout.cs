namespace GdeltSearchUI;

internal partial class GasPriceForm
{
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
                CredentialManager.LoadGasPriceBluesky,
                CredentialManager.SaveGasPriceBluesky,
                "Bluesky Account — Gas Prices");
            dlg.ShowDialog(this);
        };

        _postButton = new Button
        {
            Text = "Post",
            Dock = DockStyle.Right,
            Width = 60,
            BackColor = Color.FromArgb(0x1D, 0x83, 0xBD),
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
            Text = "EIA Weekly Retail Pump Prices",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = DarkTheme.TextMuted,
            BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 8.5f),
        };

        // Right-docked controls added in reverse visual order
        bar.Controls.Add(accountBtn);
        bar.Controls.Add(_postButton);
        bar.Controls.Add(_refreshButton);
        bar.Controls.Add(heading);
        return bar;
    }

    private Panel BuildPricePanel()
    {
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 4,
            Padding = new Padding(24, 12, 24, 12),
            BackColor = DarkTheme.Background,
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        for (var i = 0; i < 4; i++)
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 25));

        _regularLabel  = AddPriceRow(table, 0, "Regular");
        _midGradeLabel = AddPriceRow(table, 1, "Mid-Grade");
        _premiumLabel  = AddPriceRow(table, 2, "Premium");
        _dieselLabel   = AddPriceRow(table, 3, "Diesel");

        return table;
    }

    private static Label AddPriceRow(TableLayoutPanel table, int row, string fuelType)
    {
        table.Controls.Add(new Label
        {
            Text = fuelType,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = DarkTheme.TextMuted,
            Font = new Font("Segoe UI", 10f),
        }, 0, row);

        var value = new Label
        {
            Text = "—",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            ForeColor = DarkTheme.TextPrimary,
            Font = new Font("Segoe UI", 14f, FontStyle.Bold),
        };
        table.Controls.Add(value, 1, row);
        return value;
    }

    private Label BuildStatusLabel()
    {
        _statusLabel = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 24,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 0, 0),
            ForeColor = DarkTheme.TextMuted,
            BackColor = DarkTheme.Surface,
            Font = new Font("Segoe UI", 8.5f),
            Text = "Loading…",
        };
        return _statusLabel;
    }
}
