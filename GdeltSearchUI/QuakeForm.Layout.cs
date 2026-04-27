namespace GdeltSearchUI;

internal partial class QuakeForm
{
    private TableLayoutPanel BuildToolbar()
    {
        var toolbar = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 48,
            Padding = new Padding(8, 8, 8, 4),
            ColumnCount = 6,
            BackColor = DarkTheme.Surface,
        };
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 64));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88));

        _magBox = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Margin = new Padding(0, 0, 4, 0),
            BackColor = DarkTheme.Input,
            ForeColor = DarkTheme.TextPrimary,
            FlatStyle = FlatStyle.Flat,
        };
        foreach (var (label, _) in MagFilters) _magBox.Items.Add(label);
        _magBox.SelectedIndex = 1; // default M4.5+
        toolbar.Controls.Add(_magBox, 0, 0);

        _timeBox = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Margin = new Padding(0, 0, 4, 0),
            BackColor = DarkTheme.Input,
            ForeColor = DarkTheme.TextPrimary,
            FlatStyle = FlatStyle.Flat,
        };
        foreach (var (label, _) in TimeRanges) _timeBox.Items.Add(label);
        _timeBox.SelectedIndex = 0; // default past 24h
        toolbar.Controls.Add(_timeBox, 1, 0);

        _autoPostCheck = new CheckBox
        {
            Text      = "Auto-post M5+",
            Dock      = DockStyle.Fill,
            ForeColor = DarkTheme.TextPrimary,
            BackColor = Color.Transparent,
            FlatStyle = FlatStyle.Flat,
            Cursor    = Cursors.Hand,
            TextAlign = ContentAlignment.MiddleCenter,
            Padding   = new Padding(4, 0, 0, 0),
        };
        _autoPostCheck.CheckedChanged += async (_, _) =>
        {
            if (_autoPostCheck.Checked)
            {
                _autoTimer.Start();
                await AutoPostAsync();
            }
            else
            {
                _autoTimer.Stop();
                SetStatus("Auto-post disabled.");
            }
        };
        toolbar.Controls.Add(_autoPostCheck, 2, 0);

        _refreshBtn = new Button
        {
            Text = "Refresh",
            Dock = DockStyle.Fill,
            BackColor = DarkTheme.AccentBlue,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 4, 0),
        };
        _refreshBtn.FlatAppearance.BorderSize = 0;
        _refreshBtn.Click += async (_, _) => await FetchAsync();
        toolbar.Controls.Add(_refreshBtn, 3, 0);

        _postBtn = new Button
        {
            Text = "Post",
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(0x1D, 0x83, 0xBD),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 4, 0),
            Enabled = false,
        };
        _postBtn.FlatAppearance.BorderSize = 0;
        _postBtn.Click += async (_, _) => await PostToBlueskyAsync();
        toolbar.Controls.Add(_postBtn, 4, 0);

        var accountBtn = new Button
        {
            Text = "⚙ Account",
            Dock = DockStyle.Fill,
            BackColor = DarkTheme.Raised,
            ForeColor = DarkTheme.TextPrimary,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
        };
        accountBtn.FlatAppearance.BorderSize = 0;
        accountBtn.Click += (_, _) =>
        {
            using var dlg = new SettingsDialog(
                CredentialManager.LoadQuakeBluesky,
                CredentialManager.SaveQuakeBluesky,
                "Bluesky Account — Earthquakes");
            dlg.ShowDialog(this);
        };
        toolbar.Controls.Add(accountBtn, 5, 0);

        return toolbar;
    }

    private DataGridView BuildGrid()
    {
        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            RowHeadersVisible = false,
            MultiSelect = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            BackgroundColor = DarkTheme.Background,
            GridColor = DarkTheme.Raised,
            BorderStyle = BorderStyle.None,
            AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
            EnableHeadersVisualStyles = false,
            Cursor = Cursors.Default,
        };

        _grid.DefaultCellStyle.BackColor          = DarkTheme.Background;
        _grid.DefaultCellStyle.ForeColor          = DarkTheme.TextPrimary;
        _grid.DefaultCellStyle.SelectionBackColor = DarkTheme.SelectionBg;
        _grid.DefaultCellStyle.SelectionForeColor = DarkTheme.TextPrimary;
        _grid.DefaultCellStyle.Padding            = new Padding(4, 3, 4, 3);

        _grid.AlternatingRowsDefaultCellStyle.BackColor          = DarkTheme.Surface;
        _grid.AlternatingRowsDefaultCellStyle.SelectionBackColor = DarkTheme.SelectionBg;

        _grid.ColumnHeadersDefaultCellStyle.BackColor          = DarkTheme.Raised;
        _grid.ColumnHeadersDefaultCellStyle.ForeColor          = DarkTheme.TextPrimary;
        _grid.ColumnHeadersDefaultCellStyle.Font               = new Font("Segoe UI", 9.5f, FontStyle.Bold);
        _grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = DarkTheme.Raised;

        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Mag",      HeaderText = "Mag",       AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 8,  DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 10f, FontStyle.Bold) } });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Location", HeaderText = "Location",  AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 52 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Depth",    HeaderText = "Depth (km)", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 12, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight } });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Time",     HeaderText = "Time",      AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 20 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Tsunami",  HeaderText = "⚠ Tsunami", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 8,  DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });

        _grid.SelectionChanged += (_, _) => _postBtn.Enabled = _grid.CurrentRow != null;

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
