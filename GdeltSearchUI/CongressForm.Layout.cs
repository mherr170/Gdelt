namespace GdeltSearchUI;

internal partial class CongressForm
{
    private Panel BuildToolbar()
    {
        var bar = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 48,
            BackColor = DarkTheme.Surface,
            Padding   = new Padding(8, 8, 8, 8),
        };

        var apiKeyBtn = new Button
        {
            Text      = "⚙ API Key",
            Dock      = DockStyle.Right,
            Width     = 84,
            BackColor = DarkTheme.Raised,
            ForeColor = DarkTheme.TextPrimary,
            FlatStyle = FlatStyle.Flat,
            Cursor    = Cursors.Hand,
        };
        apiKeyBtn.FlatAppearance.BorderSize = 0;
        apiKeyBtn.Click += (_, _) =>
        {
            var current = CredentialManager.LoadProPublicaKey() ?? "";
            var key = PromptForApiKey($"ProPublica Congress API key (current: {(current.Length > 4 ? current[..4] + "…" : current.Length > 0 ? "set" : "not set")}):");
            if (key is not null) CredentialManager.SaveProPublicaKey(key);
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
                CredentialManager.LoadCongressBluesky,
                CredentialManager.SaveCongressBluesky,
                "Bluesky Account — Congress Votes");
            dlg.ShowDialog(this);
        };

        _postBtn = new Button
        {
            Text      = "Post Selected",
            Dock      = DockStyle.Right,
            Width     = 110,
            BackColor = DarkTheme.PostButtonDefault,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor    = Cursors.Hand,
            Enabled   = false,
        };
        _postBtn.FlatAppearance.BorderSize = 0;
        _postBtn.Click += async (_, _) => await PostToBlueskyAsync();

        _refreshBtn = new Button
        {
            Text      = "Refresh",
            Dock      = DockStyle.Right,
            Width     = 70,
            BackColor = DarkTheme.AccentBlue,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor    = Cursors.Hand,
            Margin    = new Padding(0, 0, 4, 0),
        };
        _refreshBtn.FlatAppearance.BorderSize = 0;
        _refreshBtn.Click += async (_, _) => await FetchAsync();

        var heading = new Label
        {
            Text      = "ProPublica Congress API — Recent Roll Call Votes",
            Dock      = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = DarkTheme.TextMuted,
            BackColor = Color.Transparent,
            Font      = new Font("Segoe UI", 8.5f),
        };

        bar.Controls.Add(apiKeyBtn);
        bar.Controls.Add(accountBtn);
        bar.Controls.Add(_postBtn);
        bar.Controls.Add(_refreshBtn);
        bar.Controls.Add(heading);
        return bar;
    }

    private DataGridView BuildGrid()
    {
        _grid = new DataGridView
        {
            Dock                             = DockStyle.Fill,
            ReadOnly                         = true,
            AllowUserToAddRows               = false,
            AllowUserToDeleteRows            = false,
            AllowUserToResizeRows            = false,
            RowHeadersVisible                = false,
            MultiSelect                      = false,
            SelectionMode                    = DataGridViewSelectionMode.FullRowSelect,
            BackgroundColor                  = DarkTheme.Background,
            GridColor                        = DarkTheme.Raised,
            BorderStyle                      = BorderStyle.None,
            AutoSizeRowsMode                 = DataGridViewAutoSizeRowsMode.AllCells,
            ColumnHeadersHeightSizeMode      = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
            EnableHeadersVisualStyles        = false,
            Cursor                           = Cursors.Default,
        };

        _grid.DefaultCellStyle.BackColor          = DarkTheme.Background;
        _grid.DefaultCellStyle.ForeColor          = DarkTheme.TextPrimary;
        _grid.DefaultCellStyle.SelectionBackColor = DarkTheme.SelectionBg;
        _grid.DefaultCellStyle.SelectionForeColor = DarkTheme.TextPrimary;
        _grid.DefaultCellStyle.WrapMode           = DataGridViewTriState.True;
        _grid.DefaultCellStyle.Padding            = new Padding(4, 3, 4, 3);

        _grid.AlternatingRowsDefaultCellStyle.BackColor          = DarkTheme.Surface;
        _grid.AlternatingRowsDefaultCellStyle.SelectionBackColor = DarkTheme.SelectionBg;

        _grid.ColumnHeadersDefaultCellStyle.BackColor          = DarkTheme.Raised;
        _grid.ColumnHeadersDefaultCellStyle.ForeColor          = DarkTheme.TextPrimary;
        _grid.ColumnHeadersDefaultCellStyle.Font               = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        _grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = DarkTheme.Raised;

        var centerStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter };
        var rightStyle  = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight };

        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Chamber",  HeaderText = "Chamber",  AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 8,  DefaultCellStyle = centerStyle });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Date",     HeaderText = "Date",     AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 10, DefaultCellStyle = centerStyle });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Roll",     HeaderText = "Roll #",   AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 6,  DefaultCellStyle = rightStyle });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Question", HeaderText = "Question", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 42 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Result",   HeaderText = "Result",   AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 10, DefaultCellStyle = centerStyle });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Tally",    HeaderText = "Yes / No", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 10, DefaultCellStyle = centerStyle });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Dem",      HeaderText = "Dem",      AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 7,  DefaultCellStyle = centerStyle });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Rep",      HeaderText = "Rep",      AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 7,  DefaultCellStyle = centerStyle });

        _grid.SelectionChanged += (_, _) => UpdatePostButton();

        return _grid;
    }

    private StatusStrip BuildStatusBar()
    {
        _statusLabel = new ToolStripStatusLabel { Spring = true, TextAlign = ContentAlignment.MiddleLeft, ForeColor = DarkTheme.TextMuted };
        _progress    = new ToolStripProgressBar { Visible = false, Width = 120 };
        var bar = new StatusStrip { BackColor = DarkTheme.Surface };
        bar.Items.Add(_statusLabel);
        bar.Items.Add(_progress);
        return bar;
    }
}
