namespace GdeltSearchUI;

internal partial class StockForm
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
                CredentialManager.LoadStockBluesky,
                CredentialManager.SaveStockBluesky,
                "Bluesky Account — Stock Market");
            dlg.ShowDialog(this);
        };

        _postBtn = new Button
        {
            Text      = "Post",
            Dock      = DockStyle.Right,
            Width     = 150,
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
            Text      = "Yahoo Finance — US Index Closing Values",
            Dock      = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = DarkTheme.TextMuted,
            BackColor = Color.Transparent,
            Font      = new Font("Segoe UI", 8.5f),
        };

        bar.Controls.Add(accountBtn);
        bar.Controls.Add(_postBtn);
        bar.Controls.Add(_refreshBtn);
        bar.Controls.Add(heading);
        return bar;
    }

    private SplitContainer BuildMainPanel()
    {
        var split = new SplitContainer
        {
            Dock             = DockStyle.Fill,
            Orientation      = Orientation.Horizontal,
            SplitterWidth    = 6,
            BackColor        = DarkTheme.Background,
        };

        // Top: index grid
        _grid = new DataGridView
        {
            Dock                        = DockStyle.Fill,
            ReadOnly                    = true,
            AllowUserToAddRows          = false,
            AllowUserToDeleteRows       = false,
            AllowUserToResizeRows       = false,
            RowHeadersVisible           = false,
            MultiSelect                 = false,
            SelectionMode               = DataGridViewSelectionMode.FullRowSelect,
            BackgroundColor             = DarkTheme.Background,
            GridColor                   = DarkTheme.Raised,
            BorderStyle                 = BorderStyle.None,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
            EnableHeadersVisualStyles   = false,
            Cursor                      = Cursors.Default,
        };
        _grid.DefaultCellStyle.BackColor          = DarkTheme.Background;
        _grid.DefaultCellStyle.ForeColor          = DarkTheme.TextPrimary;
        _grid.DefaultCellStyle.SelectionBackColor = DarkTheme.SelectionBg;
        _grid.DefaultCellStyle.SelectionForeColor = DarkTheme.TextPrimary;
        _grid.DefaultCellStyle.Padding            = new Padding(6, 4, 6, 4);
        _grid.AlternatingRowsDefaultCellStyle.BackColor          = DarkTheme.Surface;
        _grid.AlternatingRowsDefaultCellStyle.SelectionBackColor = DarkTheme.SelectionBg;
        _grid.ColumnHeadersDefaultCellStyle.BackColor          = DarkTheme.Raised;
        _grid.ColumnHeadersDefaultCellStyle.ForeColor          = DarkTheme.TextPrimary;
        _grid.ColumnHeadersDefaultCellStyle.Font               = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        _grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = DarkTheme.Raised;

        var rightStyle  = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight };
        var centerStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter };

        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Index",  HeaderText = "Index",       AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 30 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Price",  HeaderText = "Close",       AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 20, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Font = new Font("Segoe UI", 11f, FontStyle.Bold) } });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Change", HeaderText = "Day Change",  AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 20, DefaultCellStyle = rightStyle });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Prev",   HeaderText = "Prior Close", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 20, DefaultCellStyle = rightStyle });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Time",   HeaderText = "Updated",     AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 10, DefaultCellStyle = centerStyle });

        split.Panel1.Controls.Add(_grid);

        // Bottom: intraday chart
        _chartBox = new PictureBox
        {
            Dock     = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.FromArgb(0x1E, 0x1E, 0x1E),
        };
        split.Panel2.Controls.Add(_chartBox);

        return split;
    }

    private Label BuildStatusLabel() => _statusLabel = CreateStatusLabel();
}
