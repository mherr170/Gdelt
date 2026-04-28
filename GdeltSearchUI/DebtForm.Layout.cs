namespace GdeltSearchUI;

internal partial class DebtForm
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
                CredentialManager.LoadDebtBluesky,
                CredentialManager.SaveDebtBluesky,
                "Bluesky Account — National Debt");
            dlg.ShowDialog(this);
        };

        _postButton = new Button
        {
            Text = "Post",
            Dock = DockStyle.Right,
            Width = 150,
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
            Text = "US Treasury — Debt to the Penny",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = DarkTheme.TextMuted,
            BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 8.5f),
        };

        bar.Controls.Add(accountBtn);
        bar.Controls.Add(_postButton);
        bar.Controls.Add(_refreshButton);
        bar.Controls.Add(heading);
        return bar;
    }

    private Panel BuildDebtPanel()
    {
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 4,
            Padding = new Padding(24, 12, 24, 12),
            BackColor = DarkTheme.Background,
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        for (var i = 0; i < 4; i++)
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 25));

        (_totalLabel,    _totalDelta)    = AddRow(table, 0, "Total Public Debt");
        (_publicLabel,   _publicDelta)   = AddRow(table, 1, "Held by Public");
        (_intragovLabel, _intragovDelta) = AddRow(table, 2, "Intragov Holdings");
        (_percentLabel,  _percentDelta)  = AddRow(table, 3, "Day-over-Day %");

        return table;
    }

    private static (Label value, Label delta) AddRow(TableLayoutPanel table, int row, string fieldName)
    {
        table.Controls.Add(new Label
        {
            Text = fieldName,
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

        var delta = new Label
        {
            Text = "",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            ForeColor = DarkTheme.TextMuted,
            Font = new Font("Segoe UI", 9.5f),
            Padding = new Padding(8, 0, 0, 0),
        };
        table.Controls.Add(delta, 2, row);
        return (value, delta);
    }

    private Label BuildStatusLabel() => _statusLabel = CreateStatusLabel();
}
